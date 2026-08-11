using System.Data;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace AWms.Infrastructure.Services;

/// <summary>
/// 编号服务：Sequence 表 + PG 原子自增（INSERT ... ON CONFLICT DO UPDATE ... RETURNING，行锁）。
/// 复验意见 B-03：禁用 `{0}`+无名参数，改用 Npgsql 命名参数（@name + NpgsqlParameter）。
/// </summary>
public class NumberingService : INumberService
{
    private readonly AWmsDbContext _db;
    private readonly Dictionary<string, NumberRule> _rules = new();

    public IReadOnlyDictionary<string, NumberRule> Rules => _rules;

    public NumberingService(AWmsDbContext db)
    {
        _db = db;
        RegisterDefaults();
    }

    private void RegisterDefaults()
    {
        Register(new NumberRule { Type = "INBOUND_ORDER", ScopeKey = "PO", Prefix = "PO", DateFormat = "yyyyMMdd", SeqLength = 4, ResetPeriod = NumberResetPeriod.DAILY, MaxValue = 9999 });
        Register(new NumberRule { Type = "INBOUND_ORDER", ScopeKey = "PR", Prefix = "PR", DateFormat = "yyyyMMdd", SeqLength = 4, ResetPeriod = NumberResetPeriod.DAILY, MaxValue = 9999 });
        Register(new NumberRule { Type = "INBOUND_ORDER", ScopeKey = "OT", Prefix = "OT", DateFormat = "yyyyMMdd", SeqLength = 4, ResetPeriod = NumberResetPeriod.DAILY, MaxValue = 9999 });
        Register(new NumberRule { Type = "RECEIPT", ScopeKey = "GLOBAL", Prefix = "RCP", DateFormat = "yyyyMMdd", SeqLength = 4, ResetPeriod = NumberResetPeriod.DAILY, MaxValue = 9999 });
        Register(new NumberRule { Type = "BATCH", ScopeKey = "MATERIAL", Prefix = null, DateFormat = "yyMMdd", SeqLength = 3, ResetPeriod = NumberResetPeriod.DAILY, MaxValue = 999, DynamicScope = true });
        Register(new NumberRule { Type = "TXN_GROUP", ScopeKey = "GLOBAL", Prefix = null, DateFormat = "yyMMdd", SeqLength = 9, ResetPeriod = NumberResetPeriod.DAILY, MaxValue = 999999999 });
        Register(new NumberRule { Type = "UNIQUE_CODE", ScopeKey = "GLOBAL", Prefix = "BOX", DateFormat = "yyyyMMdd", SeqLength = 4, ResetPeriod = NumberResetPeriod.DAILY, MaxValue = 9999 });
        Register(new NumberRule { Type = "IMPORT_TASK", ScopeKey = "GLOBAL", Prefix = "IMP", DateFormat = "yyyyMMdd", SeqLength = 4, ResetPeriod = NumberResetPeriod.DAILY, MaxValue = 9999 });
    }

    /// <summary>注册/覆盖编号规则（规则变更只影响新号）。</summary>
    public void Register(NumberRule rule)
    {
        if (rule.MaxValue <= 0)
            throw new ArgumentException($"Rule {rule.Type}:{rule.ScopeKey} 的 MaxValue 必须为正数");
        _rules[$"{rule.Type}:{rule.ScopeKey}"] = rule;
    }

    public Task<string> NextAsync(string type, string? scopeKey = null) => NextAsyncCore(type, scopeKey, null);

    /// <summary>原子取号：与外部事务同一连接/事务（规范 2.9：与业务同一事务）。</summary>
    public async Task<string> NextAsyncCore(string type, string? scopeKey, IDbContextTransaction? externalTx)
    {
        var (rule, actualScopeKey) = ResolveRule(type, scopeKey);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var connection = _db.Database.GetDbConnection();
        var needOpen = connection.State != ConnectionState.Open;
        if (needOpen)
            await connection.OpenAsync();

        try
        {
            long lastNo;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO "Sequences" ("Id", "Type", "ScopeKey", "BizDate", "LastNo")
                    VALUES (@id, @type, @scopeKey, @bizDate, 1)
                    ON CONFLICT ("Type", "ScopeKey", "BizDate")
                    DO UPDATE SET "LastNo" = "Sequences"."LastNo" + 1
                    RETURNING "LastNo"
                    """;
                cmd.Parameters.Add(new NpgsqlParameter("id", Guid.CreateVersion7()));
                cmd.Parameters.Add(new NpgsqlParameter("type", type));
                cmd.Parameters.Add(new NpgsqlParameter("scopeKey", actualScopeKey));
                cmd.Parameters.Add(new NpgsqlParameter("bizDate", today));
                if (externalTx != null)
                    cmd.Transaction = externalTx.GetDbTransaction();

                var result = await cmd.ExecuteScalarAsync();
                lastNo = result == null || result == DBNull.Value ? 1 : Convert.ToInt64(result);
            }

            if (lastNo > rule.MaxValue)
            {
                if (rule.OnExhaustion == NumberExhaustion.THROW)
                    throw new DomainException("NUMBER_EXHAUSTED", $"编号耗尽：{type}:{actualScopeKey}（{today}）", 500);
                throw new DomainException("NUMBER_EXHAUSTED", $"编号耗尽且不允许回绕：{type}:{actualScopeKey}（{today}）", 500);
            }

            return FormatNumber(rule, today, lastNo);
        }
        finally
        {
            if (needOpen)
                await connection.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<string>> NextNAsync(string type, int count, string? scopeKey = null)
    {
        if (count <= 0)
            throw new ArgumentException("count 必须为正数", nameof(count));

        var results = new List<string>(count);
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, CancellationToken.None);
        try
        {
            for (var i = 0; i < count; i++)
            {
                results.Add(await NextAsyncCore(type, scopeKey, tx));
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        return results;
    }

    private (NumberRule rule, string scopeKey) ResolveRule(string type, string? scopeKey)
    {
        if (scopeKey == null)
        {
            foreach (var r in _rules.Values)
            {
                if (r.Type == type) return (r, r.ScopeKey);
            }
            throw new DomainException("VALIDATION_ERROR", $"未注册编号规则：{type}", 400);
        }

        var fullKey = $"{type}:{scopeKey}";
        if (_rules.TryGetValue(fullKey, out var exact))
            return (exact, scopeKey);

        // 动态作用域（BATCH 按物料）：使用该类型的动态规则，scope 取调用方提供的值
        var dynamicRule = _rules.Values.FirstOrDefault(r => r.Type == type && r.DynamicScope);
        if (dynamicRule != null)
            return (dynamicRule, scopeKey);

        throw new DomainException("VALIDATION_ERROR", $"未注册编号规则：{fullKey}", 400);
    }

    /// <summary>组合编号：前缀-日期-序号 / 前缀-序号 / 日期+序号（通用规范 2.9）。</summary>
    public static string FormatNumber(NumberRule rule, DateOnly bizDate, long seq)
    {
        var datePart = string.IsNullOrEmpty(rule.DateFormat) ? string.Empty : bizDate.ToString(rule.DateFormat);
        var seqPart = seq.ToString().PadLeft(rule.SeqLength, '0');

        if (!string.IsNullOrEmpty(rule.Prefix) && !string.IsNullOrEmpty(datePart))
            return $"{rule.Prefix}-{datePart}-{seqPart}";
        if (!string.IsNullOrEmpty(rule.Prefix))
            return $"{rule.Prefix}-{seqPart}";
        return $"{datePart}{seqPart}";
    }
}






using AWms.Domain.Entities;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AWms.Infrastructure.Services;

public record IdempotencyReservation(bool IsFirst, IdempotencyRecord? Existing);

/// <summary>
/// 幂等记录服务（契约 2.6 / 规范 §2.4）：
/// - TryReserve：首个请求预留（Key 唯一），并发同 key 由唯一索引兜底；
/// - Complete：首个请求完成后写入响应（含错误响应）；
/// - 普通记录 TTL 24h；关键写端点的首次完成结果永久保留，避免业务成功后过期重放。
/// </summary>
public class IdempotencyService
{
    private readonly AWmsDbContext _db;

    public IdempotencyService(AWmsDbContext db) => _db = db;

    public async Task LockKeyAsync(string key, CancellationToken ct = default)
    {
        var transaction = _db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("幂等键锁必须在数据库事务内获取");
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))";
        command.Parameters.Add(new NpgsqlParameter("key", key));
        await command.ExecuteScalarAsync(ct);
    }

    public async Task<IdempotencyReservation> TryReserveAsync(
        string key,
        TimeSpan ttl,
        bool preserveCompleted = false,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await _db.IdempotencyRecords.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (existing != null &&
            existing.ExpiresAt < now &&
            !(preserveCompleted && existing.Status == AWms.Domain.Enums.IdempotencyStatus.COMPLETED))
        {
            _db.IdempotencyRecords.Remove(existing);
            await _db.SaveChangesAsync(ct);
            existing = null;
        }

        if (existing != null)
            return new IdempotencyReservation(false, existing);

        var record = new IdempotencyRecord
        {
            Key = key,
            Status = AWms.Domain.Enums.IdempotencyStatus.PENDING,
            ResponseJson = string.Empty,
            StatusCode = 0,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl)
        };
        _db.IdempotencyRecords.Add(record);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // 并发下另一进程/请求已预留：读取胜者记录
            var winner = await _db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
            return new IdempotencyReservation(false, winner);
        }
        return new IdempotencyReservation(true, record);
    }

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default) =>
        await _db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);

    public async Task CompleteAsync(string key, int statusCode, string responseJson, CancellationToken ct = default)
    {
        var record = await _db.IdempotencyRecords.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (record == null)
            return;
        record.StatusCode = statusCode;
        record.ResponseJson = responseJson;
        record.Status = AWms.Domain.Enums.IdempotencyStatus.COMPLETED;
        await _db.SaveChangesAsync(ct);
    }
}


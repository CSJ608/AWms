using System.Text.Json;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AWms.IntegrationTests;

/// <summary>filter DSL 在真实 PostgreSQL 上的翻译验证（Npgsql 参数化 + 操作符语义）。</summary>
[Collection("PostgreSql")]
public class QueryIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> MaterialFields = new(StringComparer.Ordinal) { "code", "name", "searchCode", "batchControlled", "labelType", "defaultUom", "defaultQtyPerLabel", "status", "createdAt", "updatedAt" };
    private static readonly HashSet<string> MaterialSorts = new(StringComparer.Ordinal) { "code", "name", "status", "updatedAt" };

    private readonly PostgreSqlFixture _fixture;

    public QueryIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    private AWmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseNpgsql(_fixture.ConnectionString, npgsql => npgsql.MigrationsAssembly("AWms.Infrastructure"))
            .Options;
        return new AWmsDbContext(options);
    }

    private static FilterRequest Req(string filterJson) =>
        new(null, null, null, null, null, null, null, null, null,
            JsonSerializer.Deserialize<FilterGroup>(filterJson, JsonOpts), 1, 100);

    [Fact]
    public async Task Apply_真实PG_操作符全集()
    {
        await using var db = CreateDb();
        var prefix = Guid.CreateVersion7().ToString("N")[..8];
        db.Materials.AddRange(
            new Material { Code = $"{prefix}-01", Name = "螺母 M6", SearchCode = "LM", Status = MaterialStatus.ENABLED, BatchControlled = true, DefaultQtyPerLabel = 10m, CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Material { Code = $"{prefix}-02", Name = "螺栓 M8", SearchCode = null, Status = MaterialStatus.DISABLED, BatchControlled = false, DefaultQtyPerLabel = 5m, CreatedAt = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc) },
            new Material { Code = $"{prefix}-03", Name = "垫圈", SearchCode = "DQ", Status = MaterialStatus.ENABLED, BatchControlled = true, DefaultQtyPerLabel = 20m, CreatedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var service = new QueryService();

        // contains（JsonElement 字符串）
        var contains = await service.ApplyAsync(db.Materials.AsNoTracking(), Req($$"""{"op":"and","conditions":[{"field":"code","op":"contains","value":"{{prefix}}"}]}"""), MaterialFields, MaterialSorts, "code");
        Assert.Equal(3, contains.Result.Total);

        // 枚举 in + 数值 gte 组合
        var combo = await service.ApplyAsync(db.Materials.AsNoTracking(), Req($$"""{"op":"and","conditions":[{"field":"status","op":"in","value":["ENABLED"]},{"field":"defaultQtyPerLabel","op":"gte","value":"10"}]}"""), MaterialFields, MaterialSorts, "code");
        Assert.Equal(2, combo.Result.Total); // -01 与 -03（ENABLED 且 qty>=10）

        // isNull 可空字符串
        var isNull = await service.ApplyAsync(db.Materials.AsNoTracking(), Req("""{"op":"and","conditions":[{"field":"searchCode","op":"isNull","value":null}]}"""), MaterialFields, MaterialSorts, "code");
        Assert.Single(isNull.Result.Items); // 仅 -02

        // 日期 between（上界含当天）
        var between = await service.ApplyAsync(db.Materials.AsNoTracking(), Req("""{"op":"and","conditions":[{"field":"createdAt","op":"between","value":["2026-08-01T00:00:00Z","2026-08-10T00:00:00Z"]}]}"""), MaterialFields, MaterialSorts, "code");
        Assert.Equal(2, between.Result.Total);

        // 嵌套 or 组
        var nested = await service.ApplyAsync(db.Materials.AsNoTracking(), Req($$"""{"op":"or","conditions":[],"groups":[{"op":"and","conditions":[{"field":"code","op":"eq","value":"{{prefix}}-02"}]},{"op":"and","conditions":[{"field":"name","op":"contains","value":"垫圈"}]}]}"""), MaterialFields, MaterialSorts, "code");
        Assert.Equal(2, nested.Result.Total);
    }
}


using System.Text.Json;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AWms.Tests.Services;

public class QueryServiceTests
{
    private static readonly HashSet<string> MaterialFields = new(StringComparer.Ordinal) { "code", "name", "searchCode", "batchControlled", "labelType", "defaultUom", "defaultQtyPerLabel", "status", "createdAt", "updatedAt" };
    private static readonly HashSet<string> MaterialSorts = new(StringComparer.Ordinal) { "code", "name", "status", "updatedAt" };

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static FilterRequest Req(string? filterJson = null, string? sortJson = null) =>
        new(null, null, null, null, null, null, null, null,
            sortJson == null ? null : JsonSerializer.Deserialize<List<SortOption>>(sortJson, JsonOpts),
            filterJson == null ? null : JsonSerializer.Deserialize<FilterGroup>(filterJson, JsonOpts),
            1, 100);

    private static AWmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AWmsDbContext(options);
    }

    private static async Task<List<Material>> RunAsync(AWmsDbContext db, FilterRequest request)
    {
        var service = new QueryService();
        var (_, result) = await service.ApplyAsync(
            db.Materials.AsNoTracking(), request, MaterialFields, MaterialSorts, "code", "asc");
        return result.Items.ToList();
    }

    [Fact]
    public async Task Apply_白名单外字段_抛400()
    {
        var db = CreateDb();
        var service = new QueryService();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "hack;drop", "op": "eq", "value": "x" } ] }""");

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.ApplyAsync(
            db.Materials.AsNoTracking(), request, MaterialFields, MaterialSorts, "code", "asc"));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Apply_白名单外操作符_抛400()
    {
        var db = CreateDb();
        var service = new QueryService();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "code", "op": "regex", "value": "x" } ] }""");

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.ApplyAsync(
            db.Materials.AsNoTracking(), request, MaterialFields, MaterialSorts, "code", "asc"));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Apply_非法排序字段_抛400()
    {
        var db = CreateDb();
        var service = new QueryService();
        var request = Req(sortJson: """[ { "field": "id;drop", "dir": "asc" } ]""");

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.ApplyAsync(
            db.Materials.AsNoTracking(), request, MaterialFields, MaterialSorts, "code", "asc"));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Apply_非法排序方向_抛400()
    {
        var db = CreateDb();
        var service = new QueryService();
        var request = Req(sortJson: """[ { "field": "code", "dir": "sideways" } ]""");

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.ApplyAsync(
            db.Materials.AsNoTracking(), request, MaterialFields, MaterialSorts, "code", "asc"));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Fact]
    public async Task Apply_contains_JsonElement字符串_真实筛选()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "MAT-001", Name = "螺母 M6" },
            new Material { Code = "MAT-002", Name = "螺栓 M8" });
        await db.SaveChangesAsync();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "name", "op": "contains", "value": "螺母" } ] }""");

        var items = await RunAsync(db, request);

        Assert.Single(items);
        Assert.Equal("MAT-001", items[0].Code);
    }

    [Fact]
    public async Task Apply_startsWith_JsonElement字符串_真实筛选()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "MAT-001", Name = "螺母 M6" },
            new Material { Code = "ABC-001", Name = "螺栓" });
        await db.SaveChangesAsync();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "code", "op": "startsWith", "value": "MAT" } ] }""");

        var items = await RunAsync(db, request);

        Assert.Single(items);
    }

    [Fact]
    public async Task Apply_in_枚举数组筛选()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "A", Status = MaterialStatus.ENABLED },
            new Material { Code = "B", Status = MaterialStatus.DISABLED });
        await db.SaveChangesAsync();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "status", "op": "in", "value": ["ENABLED"] } ] }""");

        var items = await RunAsync(db, request);

        Assert.Single(items);
        Assert.Equal("A", items[0].Code);
    }

    [Fact]
    public async Task Apply_notIn_排除数组()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "A", Status = MaterialStatus.ENABLED },
            new Material { Code = "B", Status = MaterialStatus.DISABLED });
        await db.SaveChangesAsync();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "status", "op": "notIn", "value": ["DISABLED"] } ] }""");

        var items = await RunAsync(db, request);

        Assert.Single(items);
        Assert.Equal("A", items[0].Code);
    }

    [Fact]
    public async Task Apply_between_DateOnly上界含当天()
    {
        var db = CreateDb();
        var material = new Material { Code = "M1" };
        db.Materials.Add(material);
        db.Batches.AddRange(
            new Batch { MaterialId = material.Id, MaterialCode = "M1", BatchNo = "1", ProductionDate = new DateOnly(2026, 8, 9) },
            new Batch { MaterialId = material.Id, MaterialCode = "M1", BatchNo = "2", ProductionDate = new DateOnly(2026, 8, 10) },
            new Batch { MaterialId = material.Id, MaterialCode = "M1", BatchNo = "3", ProductionDate = new DateOnly(2026, 8, 11) });
        await db.SaveChangesAsync();
        var service = new QueryService();
        var request = new FilterRequest(null, null, null, null, null, null, null, null, null,
            JsonSerializer.Deserialize<FilterGroup>("""{ "op": "and", "conditions": [ { "field": "productionDate", "op": "between", "value": ["2026-08-09", "2026-08-10"] } ] }""", JsonOpts), 1, 100);

        var (_, result) = await service.ApplyAsync(
            db.Batches.AsNoTracking(), request,
            new HashSet<string>(StringComparer.Ordinal) { "productionDate" },
            new HashSet<string>(StringComparer.Ordinal) { "batchNo" }, "batchNo", "asc");

        // 上界含当天：8-09 与 8-10 都应命中，8-11 不命中
        Assert.Equal(2, result.Total);
        Assert.DoesNotContain(result.Items, b => b.BatchNo == "3");
    }

    [Fact]
    public async Task Apply_isNull_值类型不500_恒false()
    {
        var db = CreateDb();
        db.Materials.Add(new Material { Code = "A", BatchControlled = true });
        await db.SaveChangesAsync();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "batchControlled", "op": "isNull", "value": null } ] }""");

        var items = await RunAsync(db, request);

        Assert.Empty(items); // 非空值类型 isNull 恒 false
    }

    [Fact]
    public async Task Apply_isNotNull_值类型_恒true()
    {
        var db = CreateDb();
        db.Materials.Add(new Material { Code = "A", BatchControlled = true });
        await db.SaveChangesAsync();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "batchControlled", "op": "isNotNull", "value": null } ] }""");

        var items = await RunAsync(db, request);

        Assert.Single(items);
    }

    [Fact]
    public async Task Apply_isNull_可空字符串字段()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "A", SearchCode = null },
            new Material { Code = "B", SearchCode = "LM" });
        await db.SaveChangesAsync();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "searchCode", "op": "isNull", "value": null } ] }""");

        var items = await RunAsync(db, request);

        Assert.Single(items);
        Assert.Equal("A", items[0].Code);
    }

    [Fact]
    public async Task Apply_数值筛选_decimal_gte()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "A", DefaultQtyPerLabel = 5m },
            new Material { Code = "B", DefaultQtyPerLabel = 10m });
        await db.SaveChangesAsync();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "defaultQtyPerLabel", "op": "gte", "value": "10" } ] }""");

        var items = await RunAsync(db, request);

        Assert.Single(items);
        Assert.Equal("B", items[0].Code);
    }

    [Fact]
    public async Task Apply_日期筛选_datetime_between()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "A", CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Material { Code = "B", CreatedAt = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc) },
            new Material { Code = "C", CreatedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "createdAt", "op": "between", "value": ["2026-08-01T00:00:00Z", "2026-08-10T00:00:00Z"] } ] }""");

        var items = await RunAsync(db, request);

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Apply_嵌套and_or_合并()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "A", Status = MaterialStatus.ENABLED, BatchControlled = true },
            new Material { Code = "B", Status = MaterialStatus.ENABLED, BatchControlled = false },
            new Material { Code = "C", Status = MaterialStatus.DISABLED, BatchControlled = true });
        await db.SaveChangesAsync();
        // (status=ENABLED AND batchControlled=true) OR (code= C)
        var request = Req("""
            { "op": "or", "conditions": [],
              "groups": [
                { "op": "and", "conditions": [ { "field": "status", "op": "eq", "value": "ENABLED" }, { "field": "batchControlled", "op": "eq", "value": true } ] },
                { "op": "and", "conditions": [ { "field": "code", "op": "eq", "value": "C" } ] }
              ] }
            """);

        var items = await RunAsync(db, request);

        Assert.Equal(2, items.Count);
        Assert.Contains(items, m => m.Code == "A");
        Assert.Contains(items, m => m.Code == "C");
    }

    [Fact]
    public async Task Apply_枚举eq筛选()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "A", Status = MaterialStatus.ENABLED },
            new Material { Code = "B", Status = MaterialStatus.DISABLED });
        await db.SaveChangesAsync();
        var request = Req("""{ "op": "and", "conditions": [ { "field": "status", "op": "eq", "value": "DISABLED" } ] }""");

        var items = await RunAsync(db, request);

        Assert.Single(items);
        Assert.Equal("B", items[0].Code);
    }

    [Fact]
    public async Task Apply_主数据默认排序_codeAsc_idAsc_兜底()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "B" },
            new Material { Code = "A" },
            new Material { Code = "A" });
        await db.SaveChangesAsync();
        var service = new QueryService();
        var request = new FilterRequest(null, null, null, null, null, null, null, null, null, null, 1, 10);

        var (_, result) = await service.ApplyAsync(
            db.Materials.AsNoTracking(), request, MaterialFields, MaterialSorts, "code", "asc");

        Assert.Equal(3, result.Total);
        Assert.Equal("A", result.Items[0].Code);
        Assert.Equal("A", result.Items[1].Code);
        Assert.Equal("B", result.Items[2].Code);
    }

    [Fact]
    public async Task Apply_用户排序_追加idDesc_兜底()
    {
        var db = CreateDb();
        var m1 = new Material { Code = "A" };
        var m2 = new Material { Code = "A" };
        var m3 = new Material { Code = "B" };
        db.Materials.AddRange(m1, m2, m3);
        await db.SaveChangesAsync();
        var service = new QueryService();
        var request = Req(sortJson: """[ { "field": "code", "dir": "asc" } ]""");

        var (_, result) = await service.ApplyAsync(
            db.Materials.AsNoTracking(), request, MaterialFields, MaterialSorts, "code", "asc");

        // id DESC 兜底：两个 A 中 id 较大者在前（InMemory 不保证插入序=id 序，只断言兜底语义）
        Assert.Equal("A", result.Items[0].Code);
        Assert.Equal("A", result.Items[1].Code);
        Assert.True(result.Items[0].Id.CompareTo(result.Items[1].Id) > 0, "同值字段按 id DESC 兜底");
        Assert.Equal("B", result.Items[2].Code);
    }
}



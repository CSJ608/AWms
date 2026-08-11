using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AWms.IntegrationTests;

/// <summary>编号服务 PG 实跑：原子自增 / 并发取号 / 耗尽 / 格式（复验意见 B-03，不 Skip）。</summary>
// 每个测试类一个容器实例（规范 §5.4），避免跨类数据污染
public class NumberingIntegrationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public NumberingIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    private AWmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseNpgsql(_fixture.ConnectionString, npgsql => npgsql.MigrationsAssembly("AWms.Infrastructure"))
            .Options;
        return new AWmsDbContext(options);
    }

    [Fact]
    public async Task NextAsync_并发50取号_无重复无异常()
    {
        var results = new string[50];
        await Parallel.ForAsync(0, 50, async (i, ct) =>
        {
            await using var db = CreateDb();
            var service = new NumberingService(db);
            results[i] = await service.NextAsync("IMPORT_TASK");
        });

        Assert.Equal(50, results.Distinct().Count());
        Assert.All(results, r => Assert.Matches(@"^IMP-\d{8}-\d{4}$", r));
    }

    [Fact]
    public async Task NextAsync_耗尽_抛NUMBER_EXHAUSTED()
    {
        await using var db = CreateDb();
        var service = new NumberingService(db);
        var type = $"TEST_EXHAUST_{Guid.CreateVersion7():N}";
        service.Register(new NumberRule { Type = type, ScopeKey = "GLOBAL", Prefix = "T", DateFormat = "yyyyMMdd", SeqLength = 1, MaxValue = 2 });

        var n1 = await service.NextAsync(type);
        var n2 = await service.NextAsync(type);
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        Assert.Equal($"T-{today}-1", n1);
        Assert.Equal($"T-{today}-2", n2);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.NextAsync(type));
        Assert.Equal("NUMBER_EXHAUSTED", ex.Code);
        Assert.Equal(500, ex.StatusCode);
    }

    [Fact]
    public async Task NextAsync_BATCH格式_物料作用域()
    {
        await using var db = CreateDb();
        var service = new NumberingService(db);
        var materialId = Guid.CreateVersion7().ToString();

        var result = await service.NextAsync("BATCH", materialId);

        Assert.Matches(@"^\d{9}$", result); // yyMMdd(6) + 3 位 = 9 位
        Assert.Equal(9, result.Length);
    }

    [Fact]
    public async Task NextAsync_TXN_GROUP_15位()
    {
        await using var db = CreateDb();
        var service = new NumberingService(db);

        var result = await service.NextAsync("TXN_GROUP");

        Assert.Equal(15, result.Length);
        Assert.Matches(@"^\d{15}$", result);
    }

    [Fact]
    public async Task NextNAsync_批量分配_连续无重复()
    {
        await using var db = CreateDb();
        var service = new NumberingService(db);

        var results = await service.NextNAsync("IMPORT_TASK", 10);

        Assert.Equal(10, results.Count);
        Assert.Equal(10, results.Distinct().Count());
        Assert.All(results, r => Assert.Matches(@"^IMP-\d{8}-\d{4}$", r));
    }
}




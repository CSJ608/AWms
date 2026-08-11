using AWms.Domain.Entities;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AWms.Tests.Services;

public class IdempotencyServiceTests
{
    private static AWmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AWmsDbContext(options);
    }

    [Fact]
    public async Task TryReserveAsync_新key_返回首个并预留()
    {
        var db = CreateDb();
        var service = new IdempotencyService(db);

        var reservation = await service.TryReserveAsync("key-1", TimeSpan.FromHours(24));

        Assert.True(reservation.IsFirst);
        Assert.Equal("key-1", reservation.Existing!.Key);
        Assert.Empty(reservation.Existing.ResponseJson);
        Assert.Equal(1, await db.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task TryReserveAsync_已完成key_返回已有记录()
    {
        var db = CreateDb();
        var service = new IdempotencyService(db);
        await service.TryReserveAsync("key-1", TimeSpan.FromHours(24));
        await service.CompleteAsync("key-1", 201, """{"code":"OK","message":"ok","data":{}}""");

        var reservation = await service.TryReserveAsync("key-1", TimeSpan.FromHours(24));

        Assert.False(reservation.IsFirst);
        Assert.Equal(201, reservation.Existing!.StatusCode);
        Assert.Contains("OK", reservation.Existing.ResponseJson);
    }

    [Fact]
    public async Task CompleteAsync_写入响应()
    {
        var db = CreateDb();
        var service = new IdempotencyService(db);
        await service.TryReserveAsync("key-1", TimeSpan.FromHours(24));

        await service.CompleteAsync("key-1", 409, """{"code":"MATERIAL_CODE_DUPLICATED","message":"重复","data":null}""");

        var record = await service.GetAsync("key-1");
        Assert.Equal(409, record!.StatusCode);
        Assert.Contains("MATERIAL_CODE_DUPLICATED", record.ResponseJson);
    }

    [Fact]
    public async Task TryReserveAsync_过期记录_清理后可重放()
    {
        var db = CreateDb();
        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Key = "expired-key",
            ResponseJson = """{"code":"OK"}""",
            StatusCode = 200,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();
        var service = new IdempotencyService(db);

        var reservation = await service.TryReserveAsync("expired-key", TimeSpan.FromHours(24));

        Assert.True(reservation.IsFirst); // 过期后可重放
        Assert.Equal(1, await db.IdempotencyRecords.CountAsync());
    }
}

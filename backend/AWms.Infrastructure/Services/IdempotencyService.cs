using AWms.Domain.Entities;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AWms.Infrastructure.Services;

public record IdempotencyReservation(bool IsFirst, IdempotencyRecord? Existing);

/// <summary>
/// 幂等记录服务（契约 2.6 / 规范 §2.4）：
/// - TryReserve：首个请求预留（Key 唯一），并发同 key 由唯一索引兜底；
/// - Complete：首个请求完成后写入响应（含错误响应）；
/// - TTL 24h，过期记录清理后可重放。
/// </summary>
public class IdempotencyService
{
    private readonly AWmsDbContext _db;

    public IdempotencyService(AWmsDbContext db) => _db = db;

    public async Task<IdempotencyReservation> TryReserveAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // 清理过期记录（用跟踪删除以兼容 InMemory 单测与 PG 双跑）
        var expired = await _db.IdempotencyRecords.Where(x => x.ExpiresAt < now).ToListAsync(ct);
        if (expired.Count > 0)
        {
            _db.IdempotencyRecords.RemoveRange(expired);
            await _db.SaveChangesAsync(ct);
        }

        var existing = await _db.IdempotencyRecords.FirstOrDefaultAsync(x => x.Key == key, ct);
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


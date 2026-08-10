namespace AWms.Domain.Entities;

public class IdempotencyRecord
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

namespace AWms.Domain.Entities;

public class Source
{
    public Guid Id { get; set; }
    public SourceType Type { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SearchCode { get; set; }
    public MaterialStatus Status { get; set; } = MaterialStatus.ENABLED;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

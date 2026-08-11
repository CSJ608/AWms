namespace AWms.Domain.Entities;

public class Batch
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public string? SourceBatchNo { get; set; }
    public string? SourceType { get; set; }
    public string? SourceCode { get; set; }
    public DateOnly? ProductionDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.ACTIVE;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Material Material { get; set; } = null!;
}

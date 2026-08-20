namespace AWms.Domain.Entities;

public class PutawayRecord
{
    public Guid Id { get; set; }
    public Guid ReceiptLineId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public decimal Quantity { get; set; }
    public Guid? RecommendedLocationId { get; set; }
    public int SourceInventoryVersion { get; set; }
    public Guid OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime PutawayAt { get; set; } = DateTime.UtcNow;

    public ReceiptLine ReceiptLine { get; set; } = null!;
}

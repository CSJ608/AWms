namespace AWms.Domain.Entities;

public class ReceiptLine
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public int LineNo { get; set; }
    public Guid? OrderLineId { get; set; }
    public int? OrderLineNo { get; set; }
    public Guid MaterialId { get; set; }
    public Guid BatchId { get; set; }
    public decimal? ExpectedQty { get; set; }
    public decimal ActualQty { get; set; }
    public decimal? QtyDiff { get; set; }
    public ReceiptLineStatus Status { get; set; } = ReceiptLineStatus.RECEIVED;
    public string? SourceBatchNo { get; set; }
    public DateOnly? ProductionDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public Receipt Receipt { get; set; } = null!;
    public InboundOrderLine? OrderLine { get; set; }
    public Material Material { get; set; } = null!;
    public Batch Batch { get; set; } = null!;
}

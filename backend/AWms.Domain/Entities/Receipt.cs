namespace AWms.Domain.Entities;

public class Receipt
{
    public Guid Id { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public Guid StagingLocationId { get; set; }
    public Guid? InboundOrderId { get; set; }
    public InboundOrderType SourceDocType { get; set; }
    public string? SourceDocNo { get; set; }
    public SourceType? SourceType { get; set; }
    public string? SourceCode { get; set; }
    public ReceiptStatus Status { get; set; } = ReceiptStatus.RECEIVING;
    public Guid OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Warehouse Warehouse { get; set; } = null!;
    public Location StagingLocation { get; set; } = null!;
    public InboundOrder? InboundOrder { get; set; }
    public ICollection<ReceiptLine> Lines { get; set; } = new List<ReceiptLine>();
}

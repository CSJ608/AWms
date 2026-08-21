namespace AWms.Domain.Entities;

public class InboundOrder
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public InboundOrderType Type { get; set; }
    public Guid WarehouseId { get; set; }
    public SourceType? SourceType { get; set; }
    public string? SourceCode { get; set; }
    public InboundOrderStatus Status { get; set; } = InboundOrderStatus.CONFIRMED;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VoidedAt { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<InboundOrderLine> Lines { get; set; } = new List<InboundOrderLine>();
}

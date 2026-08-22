namespace AWms.Domain.Entities;

public class UniqueCode
{
    public Guid Id { get; set; }
    public Guid OrderLineId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public UniqueCodeStatus Status { get; set; } = UniqueCodeStatus.UNRECEIVED;
    public DateTime? ReceivedAt { get; set; }

    public InboundOrderLine OrderLine { get; set; } = null!;
}

namespace AWms.Domain.Entities;

public class InboundOrderLine
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public int LineNo { get; set; }
    public Guid MaterialId { get; set; }
    public decimal ExpectedQty { get; set; }

    public InboundOrder Order { get; set; } = null!;
    public Material Material { get; set; } = null!;
    public ICollection<UniqueCode> UniqueCodes { get; set; } = new List<UniqueCode>();
}

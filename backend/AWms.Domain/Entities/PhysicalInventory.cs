namespace AWms.Domain.Entities;

public class PhysicalInventory
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid SubjectId { get; set; }
    public decimal Quantity { get; set; }
    public int Version { get; set; }

    public Location Location { get; set; } = null!;
    public StockSubject Subject { get; set; } = null!;
}

namespace AWms.Domain.Entities;

public class StockSubject
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid MaterialId { get; set; }
    public Guid BatchId { get; set; }
    public StockSubjectStatus Status { get; set; }
    public string Uom { get; set; } = "CT";

    public Warehouse Warehouse { get; set; } = null!;
    public Material Material { get; set; } = null!;
    public Batch Batch { get; set; } = null!;
}

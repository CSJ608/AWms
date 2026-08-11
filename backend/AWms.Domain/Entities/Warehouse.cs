namespace AWms.Domain.Entities;

public class Warehouse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SearchCode { get; set; }
    public MaterialStatus Status { get; set; } = MaterialStatus.ENABLED;
    public WarehouseMgmtMode MgmtMode { get; set; } = WarehouseMgmtMode.MANUAL;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Location> Locations { get; set; } = new List<Location>();
}

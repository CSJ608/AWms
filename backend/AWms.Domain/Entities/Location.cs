namespace AWms.Domain.Entities;

public class Location
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? SearchCode { get; set; }
    public LocationType Type { get; set; } = LocationType.DEFAULT;
    public MaterialStatus Status { get; set; } = MaterialStatus.ENABLED;
    public LocationReachability Reachability { get; set; } = LocationReachability.UNIVERSAL;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Warehouse Warehouse { get; set; } = null!;
}

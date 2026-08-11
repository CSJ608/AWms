namespace AWms.Domain.Entities;

public class Material
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SearchCode { get; set; }
    public bool BatchControlled { get; set; }
    public LabelType LabelType { get; set; } = LabelType.NONE;
    public string DefaultUom { get; set; } = "CT";
    public decimal? DefaultQtyPerLabel { get; set; }
    public MaterialStatus Status { get; set; } = MaterialStatus.ENABLED;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

namespace AWms.Domain.Entities;

public class TxnGroup
{
    public Guid Id { get; set; }
    public string GroupNo { get; set; } = string.Empty;
    public TxnGroupType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

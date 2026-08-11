namespace AWms.Domain.Entities;

public class Sequence
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ScopeKey { get; set; } = string.Empty;
    public DateOnly BizDate { get; set; }
    public long LastNo { get; set; }
}

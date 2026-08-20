namespace AWms.Domain.Entities;

public class PrintJobItem
{
    public Guid Id { get; set; }
    public Guid PrintJobId { get; set; }
    public int Seq { get; set; }
    public string LabelType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ReadableText { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }

    public PrintJob PrintJob { get; set; } = null!;
}

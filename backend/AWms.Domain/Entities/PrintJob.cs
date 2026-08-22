namespace AWms.Domain.Entities;

public class PrintJob
{
    public Guid Id { get; set; }
    public string? BizType { get; set; }
    public Guid? BizId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public PrintJobStatus Status { get; set; } = PrintJobStatus.GENERATING;
    public string? FilePath { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PrintJobItem> Items { get; set; } = new List<PrintJobItem>();
}

namespace AWms.Domain.Entities;

public class ImportTask
{
    public Guid Id { get; set; }
    public string TaskNo { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FileContent { get; set; }
    public ImportTaskDirection Direction { get; set; }
    public ImportTaskStatus Status { get; set; }
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public Guid? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public bool CanExecute => FailCount == 0 && Status == ImportTaskStatus.PRECHECKED;
    public string? FailReportUrl => FailCount > 0 ? $"/api/import-export/tasks/{Id}/fail-report" : null;
    public string? FileUrl => Direction == ImportTaskDirection.EXPORT && Status == ImportTaskStatus.DONE
        ? $"/api/import-export/tasks/{Id}/file" : null;

    public ICollection<ImportTaskDetail> Details { get; set; } = new List<ImportTaskDetail>();
}

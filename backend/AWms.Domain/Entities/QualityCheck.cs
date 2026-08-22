namespace AWms.Domain.Entities;

public class QualityCheck
{
    public Guid Id { get; set; }
    public Guid ReceiptLineId { get; set; }
    public decimal CheckedQty { get; set; }
    public QualityCheckResult Result { get; set; }
    public QualityExceptionReason? ExceptionReason { get; set; }
    public string? Note { get; set; }
    public string PhotoIdsJson { get; set; } = "[]";
    public Guid OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public QualityResolutionAction? ResolutionAction { get; set; }
    public string? ResolutionNote { get; set; }
    public Guid? ResolvedBy { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public ReceiptLine ReceiptLine { get; set; } = null!;
}

namespace AWms.Domain.Entities;

public class StockLedger
{
    public Guid Id { get; set; }
    public Guid TxnGroupId { get; set; }
    public int Seq { get; set; }
    public Guid SubjectId { get; set; }
    public Guid LocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public LedgerReason Reason { get; set; }
    public string? SourceDocType { get; set; }
    public string? SourceDocNo { get; set; }
    public Guid OperatorId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public TxnGroup TxnGroup { get; set; } = null!;
}

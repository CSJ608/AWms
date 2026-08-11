namespace AWms.Domain.Entities;

public class ImportTaskDetail
{
    public Guid Id { get; set; }
    public Guid ImportTaskId { get; set; }
    public int RowNo { get; set; }
    public string? ColumnCode { get; set; }
    public string? ColumnName { get; set; }
    public string? RawValue { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMsg { get; set; }

    public ImportTask ImportTask { get; set; } = null!;
}

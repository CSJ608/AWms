using AWms.Domain.Dtos.Common;

namespace AWms.Domain.Dtos.ImportExport;

public record ExecuteRequest(Guid TaskId);

/// <summary>导出任务请求：filter/sort 遵循通用规范 2.10（filter DSL 对象 + sort 数组）。</summary>
public record ExportRequest(string ModuleCode, FilterGroup? Filter, List<SortOption>? Sort, int? PageSize);

/// <summary>导入导出任务响应；failures 固定 inline ≤200（契约导入导出 v0.2）。</summary>
public record ImportTaskResponse(
    Guid Id,
    string TaskNo,
    string ModuleCode,
    string FileName,
    string Direction,
    string Status,
    int TotalCount,
    int SuccessCount,
    int FailCount,
    bool CanExecute,
    string? FailReportUrl,
    string? FileUrl,
    Guid? OperatorId,
    string? OperatorName,
    DateTime CreatedAt,
    List<FailureDetail>? Failures = null);

public record FailureDetail(
    int RowNo,
    string? ColumnCode,
    string? ColumnName,
    string? RawValue,
    string ErrorCode,
    string ErrorMsg);

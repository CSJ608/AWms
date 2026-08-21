namespace AWms.Domain.Dtos.Receipts;

public record BatchPropsRequest(
    string? SourceBatchNo,
    string? ProductionDate,
    string? ExpiryDate,
    string? SourceType,
    string? SourceCode);

public record SubmitReceiptLineRequest(
    Guid? OrderLineId,
    Guid MaterialId,
    Guid? BatchId,
    BatchPropsRequest? BatchProps,
    string Quantity,
    IReadOnlyList<string>? UniqueCodes);

public record SubmitReceiptRequest(
    Guid WarehouseId,
    Guid StagingLocationId,
    Guid? InboundOrderId,
    string? SourceDocType,
    string? SourceDocNo,
    string? SourceType,
    string? SourceCode,
    IReadOnlyList<SubmitReceiptLineRequest> Lines,
    IReadOnlyList<Guid>? Photos);

public record ReceiptItem(
    Guid Id,
    string ReceiptNo,
    Guid WarehouseId,
    string WarehouseCode,
    Guid? InboundOrderId,
    string SourceDocType,
    string? SourceDocNo,
    string? SourceType,
    string? SourceCode,
    string Status,
    IReadOnlyList<ReceiptLineItem> Lines,
    Guid StagingLocationId,
    string StagingLocationCode,
    IReadOnlyList<Guid> Photos,
    Guid OperatorId,
    string OperatorName,
    DateTime OccurredAt);

public record ReceiptLineItem(
    Guid Id,
    int LineNo,
    Guid? OrderLineId,
    int? OrderLineNo,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    Guid BatchId,
    string BatchNo,
    string? ExpectedQty,
    string ActualQty,
    string? QtyDiff,
    string Status,
    string? SourceBatchNo,
    string? ProductionDate,
    string? ExpiryDate);

public record ReceiptSearchRequest(
    string? Status,
    Guid? WarehouseId,
    string? ReceiptNo,
    DateTime? DateFrom,
    DateTime? DateTo,
    AWms.Domain.Dtos.Common.FilterGroup? Filter,
    IReadOnlyList<AWms.Domain.Dtos.Common.SortOption>? Sort,
    int? Page,
    int? PageSize);

public record QualityTodoSearchRequest(
    Guid? WarehouseId,
    Guid? MaterialId,
    Guid? BatchId,
    string? Keyword,
    int? Page,
    int? PageSize);

public record QualityTodoItem(
    Guid ReceiptLineId,
    Guid ReceiptId,
    string ReceiptNo,
    Guid WarehouseId,
    string WarehouseCode,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    Guid BatchId,
    string BatchNo,
    string Quantity,
    DateTime ReceivedAt);

public record QualityCheckRequest(
    string Result,
    string CheckedQty,
    string? ExceptionReason,
    string? Note,
    IReadOnlyList<Guid>? PhotoIds);

public record QualityExceptionSearchRequest(
    Guid? WarehouseId,
    string? ResolutionStatus,
    string? ExceptionReason,
    string? Keyword,
    int? Page,
    int? PageSize);

public record QualityExceptionItem(
    Guid Id,
    Guid ReceiptLineId,
    string ReceiptNo,
    Guid WarehouseId,
    string WarehouseCode,
    string MaterialCode,
    string MaterialName,
    string BatchNo,
    string CheckedQty,
    string ExceptionReason,
    string? Note,
    IReadOnlyList<Guid> PhotoIds,
    Guid CheckedBy,
    string CheckedByName,
    DateTime CheckedAt,
    string? ResolutionAction,
    string? ResolutionNote,
    Guid? ResolvedBy,
    string? ResolvedByName,
    DateTime? ResolvedAt);

public record ResolveQualityCheckRequest(string Action, string? Note);

public record PutawayTodoSearchRequest(
    Guid? WarehouseId,
    Guid? MaterialId,
    Guid? BatchId,
    string? Keyword,
    int? Page,
    int? PageSize);

public record PutawayTodoItem(
    Guid ReceiptLineId,
    string ReceiptNo,
    Guid WarehouseId,
    string WarehouseCode,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    Guid BatchId,
    string BatchNo,
    string Quantity,
    string? DefaultQtyPerLabel,
    Guid FromLocationId,
    string FromLocationCode,
    int InventoryVersion);

public record LocationRecommendationItem(
    Guid LocationId,
    string LocationCode,
    string ReasonCode,
    string Reason,
    bool Recommended);

public record CreatePutawayRecordRequest(
    Guid ReceiptLineId,
    Guid ToLocationId,
    string ScannedLocationCode,
    int ExpectedInventoryVersion);

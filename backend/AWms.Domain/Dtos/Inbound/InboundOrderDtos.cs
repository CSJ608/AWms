namespace AWms.Domain.Dtos.Inbound;

public record InboundOrderItem(
    Guid Id,
    string OrderNo,
    string Type,
    Guid WarehouseId,
    string WarehouseCode,
    string? SourceType,
    string? SourceCode,
    string Status,
    IReadOnlyList<InboundOrderLineItem> Lines,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? VoidedAt,
    string? VoidedBy,
    string? VoidReason);

public record InboundOrderLineItem(
    Guid Id,
    int LineNo,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string ExpectedQty,
    string ReceivedQty,
    string RemainingQty,
    IReadOnlyList<UniqueCodeItem> UniqueCodes);

public record UniqueCodeItem(
    string Code,
    string Quantity,
    string Status,
    DateTime? ReceivedAt);

public record CreateInboundOrderRequest(
    string Type,
    Guid WarehouseId,
    string? SourceType,
    string? SourceCode,
    IReadOnlyList<CreateInboundOrderLineRequest> Lines);

public record CreateInboundOrderLineRequest(Guid MaterialId, string ExpectedQty);

public record InboundOrderSearchRequest(
    string? Type,
    Guid? WarehouseId,
    string? Status,
    string? OrderNo,
    AWms.Domain.Dtos.Common.FilterGroup? Filter,
    IReadOnlyList<AWms.Domain.Dtos.Common.SortOption>? Sort,
    int? Page,
    int? PageSize);

public record VoidInboundOrderRequest(string Reason);

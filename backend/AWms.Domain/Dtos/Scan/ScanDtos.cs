namespace AWms.Domain.Dtos.Scan;

public record ScanParseRequest(string Content, ScanContext? Context);

public record ScanContext(Guid? InboundOrderId, Guid? WarehouseId);

public record ScanResult(
    string Type,
    string? LabelType,
    ScanMaterialItem? Material,
    ScanUniqueCodeItem? UniqueCode,
    ScanBatchItem? Batch,
    ScanBatchPropsItem? BatchProps,
    string? Quantity,
    ScanDocumentItem? Document,
    ScanSourceItem? Source,
    object? External,
    IReadOnlyList<ScanWarning> Warnings,
    string? Message = null);

public record ScanMaterialItem(
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    bool BatchControlled,
    string LabelType,
    string DefaultUom,
    string? DefaultQtyPerLabel);

public record ScanUniqueCodeItem(string Code, string Status, string Quantity, DateTime? ReceivedAt);

public record ScanBatchItem(
    Guid BatchId,
    string BatchNo,
    string? SourceBatchNo,
    string? ProductionDate,
    string? ExpiryDate);

public record ScanBatchPropsItem(
    string? SourceBatchNo,
    string? ProductionDate,
    string? ExpiryDate,
    string? SourceType,
    string? SourceCode);

public record ScanDocumentItem(
    Guid InboundOrderId,
    string DocType,
    string DocNo,
    Guid WarehouseId,
    string WarehouseCode,
    string Status,
    IReadOnlyList<ScanDocumentLineItem> Lines);

public record ScanDocumentLineItem(
    Guid OrderLineId,
    int LineNo,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    string ExpectedQty,
    string ReceivedQty,
    string RemainingQty,
    IReadOnlyList<AWms.Domain.Dtos.Inbound.UniqueCodeItem> UniqueCodes);

public record ScanSourceItem(string SourceType, string SourceCode, string SourceName);

public record ScanWarning(string Code, string Message, bool Blocking);

namespace AWms.Domain.Dtos.Print;

public record PrintJobItemDto(
    string LabelType,
    string Content,
    string ReadableText,
    string? Quantity);

public record PrintJobDto(
    Guid Id,
    string? BizType,
    Guid? BizId,
    string TemplateCode,
    string Status,
    IReadOnlyList<PrintJobItemDto> Items,
    string? FileUrl,
    string? ErrorCode,
    Guid CreatedBy,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record InboundOrderQrPrintRequest(Guid InboundOrderId);

public record ExternalLabelPrintRequest(IReadOnlyList<ExternalLabelPrintItemRequest> Items);

public record ExternalLabelPrintItemRequest(Guid MaterialId, int Count, Guid? InboundOrderLineId, string? Rt, string? Rc);

public record UniqueLabelsPrintRequest(Guid InboundOrderLineId, int Count, string? QtyPerCode);

public record BatchLabelsPrintRequest(Guid ReceiptLineId, string? QtyPerLabel);

public record BatchLabelOnePrintRequest(Guid ReceiptLineId, string Quantity);

public record PrintJobSearchRequest(
    string? BizType,
    Guid? BizId,
    string? TemplateCode,
    string? Status,
    int? Page,
    int? PageSize);

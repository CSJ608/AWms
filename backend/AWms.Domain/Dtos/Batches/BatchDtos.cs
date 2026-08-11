namespace AWms.Domain.Dtos.Batches;

public record BatchItem(
    Guid Id,
    Guid MaterialId,
    string MaterialCode,
    string BatchNo,
    string? SourceBatchNo,
    string? SourceType,
    string? SourceCode,
    string? ProductionDate,
    string? ExpiryDate,
    string Status,
    DateTime CreatedAt);

namespace AWms.Domain.Dtos.Materials;

public record MaterialItem(
    Guid Id,
    string Code,
    string Name,
    string? SearchCode,
    bool BatchControlled,
    string LabelType,
    string DefaultUom,
    string? DefaultQtyPerLabel,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateMaterialRequest(
    string Code,
    string Name,
    string? SearchCode,
    bool BatchControlled,
    string LabelType,
    string DefaultUom,
    string? DefaultQtyPerLabel,
    string? Status);

public record UpdateMaterialRequest(
    string Name,
    string? SearchCode,
    bool BatchControlled,
    string LabelType,
    string DefaultUom,
    string? DefaultQtyPerLabel,
    string Status);

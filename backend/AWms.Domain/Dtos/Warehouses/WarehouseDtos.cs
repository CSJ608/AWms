namespace AWms.Domain.Dtos.Warehouses;

public record WarehouseItem(
    Guid Id,
    string Code,
    string Name,
    string? SearchCode,
    string Status,
    string MgmtMode,
    DateTime CreatedAt);

public record CreateWarehouseRequest(string Code, string Name, string? SearchCode, string? Status, string? MgmtMode);

public record UpdateWarehouseRequest(string Name, string? SearchCode, string Status);

public record LocationItem(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    string Code,
    string? SearchCode,
    string Type,
    string Status,
    string Reachability,
    DateTime CreatedAt);

public record CreateLocationRequest(string Code, string? SearchCode, string Type, string? Status);

public record UpdateLocationRequest(string Type, string? SearchCode, string Status);

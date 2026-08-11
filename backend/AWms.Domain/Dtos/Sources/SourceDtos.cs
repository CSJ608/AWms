namespace AWms.Domain.Dtos.Sources;

public record SourceItem(
    Guid Id,
    string Type,
    string Code,
    string Name,
    string? SearchCode,
    string Status,
    DateTime CreatedAt);

public record CreateSourceRequest(string Type, string Code, string Name, string? SearchCode, string? Status);

public record UpdateSourceRequest(string Name, string? SearchCode, string Status);

namespace AWms.Domain.Dtos.Common;

public record ApiResponse<T>(string Code, string Message, T? Data);

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data) => new("OK", "ok", data);
    public static ApiResponse<T> Error<T>(string code, string message) => new(code, message, default);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record FilterCondition(string Field, string Op, object? Value);

public record FilterGroup(string Op, List<FilterCondition> Conditions, List<FilterGroup>? Groups = null);

public record FilterRequest(
    string? Keyword,
    string? Code,
    string? Name,
    string? Status,
    string? Type,
    string? LabelType,
    string? MaterialId,
    string? MaterialCode,
    List<SortOption>? Sort,
    FilterGroup? Filter,
    int? Page,
    int? PageSize);

public record SortOption(string Field, string Dir);

public record FieldMeta(
    string Field,
    string LabelKey,
    string Type,
    List<string> Operators,
    string? RefResource = null,
    List<FieldOption>? Options = null);

public record FieldOption(string Value, string LabelKey);

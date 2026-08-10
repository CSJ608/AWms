using AWms.Domain.Dtos.Common;
using AWms.Domain.Entities;

namespace AWms.Domain.Interfaces;

public interface IQueryService
{
    (IQueryable<T> Query, PagedResult<T> Result) Apply<T>(
        IQueryable<T> source,
        FilterRequest request,
        IReadOnlySet<string> fieldWhitelist,
        IReadOnlySet<string> sortWhitelist,
        string defaultSortField,
        string defaultSortDir = "asc",
        bool isTimeBasedList = false) where T : class;
}

public interface INumberService
{
    Task<string> NextAsync(string type, string? scopeKey = null);
    Task<IReadOnlyList<string>> NextNAsync(string type, int count, string? scopeKey = null);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user, IReadOnlyList<string> permissions);
    string GenerateRefreshToken();
    bool ValidateExpiredToken(string token, out Guid userId, out string username);
}

public interface ITimeProvider
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

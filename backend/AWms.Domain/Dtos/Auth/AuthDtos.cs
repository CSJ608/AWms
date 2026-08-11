namespace AWms.Domain.Dtos.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    UserInfo User,
    List<string> Permissions,
    MenuGroup Menus);

public record UserInfo(
    Guid Id,
    string Username,
    string Name,
    string Status,
    List<RoleBrief> Roles);

public record RoleBrief(Guid Id, string Code, string Name);

public record MenuGroup(List<MenuDto> Web, List<MenuDto> Pda);

public record MenuDto(string Code, string TitleKey, string? GroupKey, string? ModuleCode, string? IconKey, string? Path, int Sort, List<MenuDto>? Children);

public record RefreshResponse(string Token, DateTime ExpiresAt);

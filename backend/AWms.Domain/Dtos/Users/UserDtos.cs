namespace AWms.Domain.Dtos.Users;

public record UserItem(
    Guid Id,
    string Username,
    string Name,
    string Status,
    List<UserRoleDto> Roles,
    DateTime CreatedAt);

public record UserRoleDto(Guid Id, string Code, string Name);

public record CreateUserRequest(string Username, string Name, string Password, string? Status, List<Guid>? RoleIds);

public record UpdateUserRequest(string Name, string Status);

public record AssignRolesRequest(List<Guid> RoleIds);

public record ResetPasswordRequest(string NewPassword);

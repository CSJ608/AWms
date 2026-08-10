namespace AWms.Domain.Dtos.Roles;

public record RoleItem(Guid Id, string Code, string Name, List<string> PermissionCodes, DateTime CreatedAt);

public record CreateRoleRequest(string Code, string Name, List<string>? PermissionCodes);

public record UpdateRoleRequest(string Name, List<string>? PermissionCodes);

public record AssignPermissionsRequest(List<string> PermissionCodes);

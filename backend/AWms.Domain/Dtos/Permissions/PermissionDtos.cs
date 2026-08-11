namespace AWms.Domain.Dtos.Permissions;

public record PermissionItem(Guid Id, string Code, string Name, string Category, string ModuleCode);

namespace AWms.Domain.Entities;

public class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PermissionCategory Category { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
}

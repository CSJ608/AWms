namespace AWms.Domain.Entities;

public class MenuDefinition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TitleKey { get; set; } = string.Empty;
    public string? GroupKey { get; set; }
    public string? ModuleCode { get; set; }
    public string? IconKey { get; set; }
    public string? Path { get; set; }
    public Surface Surface { get; set; }
    public int Sort { get; set; }
    public Guid? ParentId { get; set; }
    public string? RequiredPermissionCode { get; set; }

    public MenuDefinition? Parent { get; set; }
    public ICollection<MenuDefinition> Children { get; set; } = new List<MenuDefinition>();
}

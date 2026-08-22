namespace Master.Entity.Models;

public class MenuView
{
    public int MenuId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Icon { get; set; }
    public List<SubMenuView> SubMenus { get; set; } = [];
}

public class SubMenuView
{
    public int SubMenuId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? RoutePath { get; set; }
    public string? Icon { get; set; }
    public bool HasAccess { get; set; }
    public List<string> AllowedActions { get; set; } = [];
}
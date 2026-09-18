namespace Master.Entity.Models;

/// <summary>One module in the navigation rail, as the client receives it.</summary>
public class MenuView
{
    public int MenuId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Icon { get; set; }
    public string Module { get; set; } = null!;

    /// <summary>Set when the entry navigates directly instead of opening a panel.</summary>
    public string? RoutePath { get; set; }

    public bool IsSearchable { get; set; }
    public List<MenuGroupView> Groups { get; set; } = [];
}

/// <summary>A section within a module's panel. <see cref="Name"/> is null for a flat list.</summary>
public class MenuGroupView
{
    public int MenuGroupId { get; set; }
    public string Code { get; set; } = null!;
    public string? Name { get; set; }

    /// <summary>Lucide icon name for the section heading.</summary>
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
    public string Module { get; set; } = null!;

    /// <summary>Whether this screen offers an inline create action at all.</summary>
    public bool CanCreate { get; set; }

    /// <summary>What one record is called, for the create button's label.</summary>
    public string? SingularName { get; set; }

    public bool HasAccess { get; set; }

    /// <summary>
    /// The actions this user holds on this screen — view, create, edit, delete,
    /// print, export, void, approve. A screen can hide what the role cannot do.
    /// </summary>
    public List<string> AllowedActions { get; set; } = [];
}

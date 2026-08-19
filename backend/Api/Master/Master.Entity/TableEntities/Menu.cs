using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

public class Menu : AuditableEntity
{
    public int MenuId { get; set; }

    [Required(ErrorMessage = "Menu code is required.")]
    [MaxLength(50, ErrorMessage = "Menu code cannot exceed 50 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Menu name is required.")]
    [MaxLength(100, ErrorMessage = "Menu name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(50, ErrorMessage = "Icon cannot exceed 50 characters.")]
    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<SubMenu> SubMenus { get; set; } = new List<SubMenu>();
}

public class SubMenu : AuditableEntity
{
    public int SubMenuId { get; set; }

    [Required(ErrorMessage = "SubMenu code is required.")]
    [MaxLength(50, ErrorMessage = "SubMenu code cannot exceed 50 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "SubMenu name is required.")]
    [MaxLength(100, ErrorMessage = "SubMenu name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Route path cannot exceed 200 characters.")]
    public string? RoutePath { get; set; }

    [MaxLength(50, ErrorMessage = "Icon cannot exceed 50 characters.")]
    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public int MenuId { get; set; }
    public Menu Menu { get; set; } = null!;

    public ICollection<SubMenuPermission> Permissions { get; set; } = new List<SubMenuPermission>();
}

public class SubMenuPermission : AuditableEntity
{
    public int SubMenuPermissionId { get; set; }

    public int SubMenuId { get; set; }
    public SubMenu SubMenu { get; set; } = null!;

    /// <summary>Permission code in format {module}.{action} e.g. sales.view, sales.create</summary>
    [Required(ErrorMessage = "Permission code is required.")]
    [MaxLength(100, ErrorMessage = "Permission code cannot exceed 100 characters.")]
    public string PermissionCode { get; set; } = null!;

    /// <summary>Action type: view, create, edit, delete, print, export, import, AllUserData</summary>
    [Required(ErrorMessage = "Action is required.")]
    [MaxLength(20, ErrorMessage = "Action cannot exceed 20 characters.")]
    public string Action { get; set; } = null!;

    /// <summary>Module name e.g. sales, inventory, accounting</summary>
    [Required(ErrorMessage = "Module is required.")]
    [MaxLength(50, ErrorMessage = "Module cannot exceed 50 characters.")]
    public string Module { get; set; } = null!;
}
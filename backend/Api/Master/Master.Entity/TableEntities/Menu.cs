using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// What a <see cref="Menu"/> row is. One table holds all three, told apart by this
/// and by <see cref="Menu.ParentId"/>.
/// </summary>
public enum MenuType
{
    /// <summary>A module in the left rail. Always a root: <c>ParentId</c> is null.</summary>
    Rail = 0,

    /// <summary>A section inside a module's panel. Its parent is a <see cref="Rail"/>.</summary>
    Group = 1,

    /// <summary>A screen. Its parent is a <see cref="Group"/>.</summary>
    Item = 2,
}

/// <summary>
/// One node of the navigation tree — rail module, panel section, or screen.
///
/// The three levels share a table because they share a shape: all of them have a
/// code, a name, an icon, an order and an active flag, and all of them hang off a
/// parent. Splitting them into three tables meant three sets of the same columns,
/// three joins to read one menu, and a schema change every time the design grows a
/// level. <see cref="Type"/> and <see cref="ParentId"/> carry that structure
/// instead, and a deeper tree costs a row rather than a migration.
///
/// Which columns matter depends on the type:
/// <list type="bullet">
/// <item><see cref="Rail"/>: <see cref="Icon"/>, <see cref="Module"/>,
/// <see cref="IsSearchable"/>, and <see cref="RoutePath"/> only when the module has
/// no panel of its own (Home).</item>
/// <item><see cref="Group"/>: <see cref="Name"/> null means an unnamed section,
/// which the design draws as plain rows rather than a collapsible header.</item>
/// <item><see cref="Item"/>: <see cref="RoutePath"/>, <see cref="Module"/>,
/// <see cref="CanCreate"/>, <see cref="SingularName"/> and its permissions.</item>
/// </list>
/// </summary>
public class Menu : AuditableEntity
{
    public int MenuId { get; set; }

    /// <summary>Null for a rail module; otherwise the row this one hangs under.</summary>
    public int? ParentId { get; set; }

    public Menu? Parent { get; set; }

    public MenuType Type { get; set; }

    /// <summary>
    /// Stable identifier, unique among its siblings — <c>inv</c>, <c>settings-g1</c>.
    /// Code is what other code should match on; ids are database bookkeeping.
    /// </summary>
    [Required(ErrorMessage = "Menu code is required.")]
    [MaxLength(50, ErrorMessage = "Menu code cannot exceed 50 characters.")]
    public string Code { get; set; } = null!;

    /// <summary>The label. Null only on an unnamed group.</summary>
    [MaxLength(100, ErrorMessage = "Menu name cannot exceed 100 characters.")]
    public string? Name { get; set; }

    /// <summary>Lucide icon name, e.g. <c>shopping-cart</c> — not markup.</summary>
    [MaxLength(50, ErrorMessage = "Icon cannot exceed 50 characters.")]
    public string? Icon { get; set; }

    /// <summary>
    /// The permission module guarding this row. On an item that is the module of
    /// the route it opens, which is not always the module it is filed under: the
    /// design places HSN/SAC beneath Inventory while its route is guarded by
    /// <c>settings.view</c>, and the guard is what belongs here. Null on a group,
    /// which guards nothing of its own.
    /// </summary>
    [MaxLength(50, ErrorMessage = "Module cannot exceed 50 characters.")]
    public string? Module { get; set; }

    /// <summary>
    /// Where the row goes. Null on a group, on a rail module that opens a panel,
    /// and on an item whose screen is designed but not built — such an item is
    /// seeded inactive and never reaches a user.
    /// </summary>
    [MaxLength(200, ErrorMessage = "Route path cannot exceed 200 characters.")]
    public string? RoutePath { get; set; }

    /// <summary>
    /// Whether the panel offers a search box. True only where the list is long
    /// enough to need one — Reports.
    /// </summary>
    public bool IsSearchable { get; set; }

    /// <summary>Whether the row offers the inline create button the design reveals on hover.</summary>
    public bool CanCreate { get; set; }

    /// <summary>
    /// What one of these is called — "Invoice" for the Invoices row. It labels the
    /// create button ("New invoice") and cannot be derived from <see cref="Name"/>.
    /// </summary>
    [MaxLength(100, ErrorMessage = "Singular name cannot exceed 100 characters.")]
    public string? SingularName { get; set; }

    /// <summary>Order among siblings.</summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Menu> Children { get; set; } = new List<Menu>();

    public ICollection<MenuPermission> Permissions { get; set; } = new List<MenuPermission>();
}

/// <summary>
/// One action a role may hold on a menu row. A user sees the row when they hold any
/// of its permissions, and the actions they do hold come back with it, so a screen
/// can hide the buttons they cannot use.
/// </summary>
public class MenuPermission : AuditableEntity
{
    public int MenuPermissionId { get; set; }

    public int MenuId { get; set; }
    public Menu Menu { get; set; } = null!;

    /// <summary>Permission code in format {module}.{action} e.g. sales.view, sales.create</summary>
    [Required(ErrorMessage = "Permission code is required.")]
    [MaxLength(100, ErrorMessage = "Permission code cannot exceed 100 characters.")]
    public string PermissionCode { get; set; } = null!;

    /// <summary>Action type: view, create, edit, delete, print, export, import, void, approve, AllUserData</summary>
    [Required(ErrorMessage = "Action is required.")]
    [MaxLength(20, ErrorMessage = "Action cannot exceed 20 characters.")]
    public string Action { get; set; } = null!;

    /// <summary>Module name e.g. sales, inventory, accounting</summary>
    [Required(ErrorMessage = "Module is required.")]
    [MaxLength(50, ErrorMessage = "Module cannot exceed 50 characters.")]
    public string Module { get; set; } = null!;
}

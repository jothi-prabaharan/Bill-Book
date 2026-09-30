using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Apps;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

public class Permission : AuditableEntity
{
    public int PermissionId { get; set; }

    /// <summary>Format {module}.{action}, e.g. sales.create. platform.* is operator-only.</summary>
    [Required(ErrorMessage = "Permission code is required.")]
    [MaxLength(100, ErrorMessage = "Permission code cannot exceed 100 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Module is required.")]
    [MaxLength(50, ErrorMessage = "Module cannot exceed 50 characters.")]
    public string Module { get; set; } = null!;

    /// <summary>The business vertical this permission requires, if any (D-36, TK-127).</summary>
    public BusinessType? BusinessType { get; set; }

    /// <summary>Modular add-ons this permission belongs to, if any (D-36, TK-127).</summary>
    public AddOn AddOns { get; set; } = AddOn.None;

    [MaxLength(200, ErrorMessage = "Description cannot exceed 200 characters.")]
    public string? Description { get; set; }
}

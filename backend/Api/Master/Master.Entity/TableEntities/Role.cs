using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Apps;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

public class Role : AuditableEntity
{
    public int RoleId { get; set; }

    /// <summary>Null = built-in system role; set = customer-defined.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Immutable, hidden — the canonical role identity. Code keys on this.</summary>
    [Required(ErrorMessage = "System name is required.")]
    [MaxLength(100, ErrorMessage = "System name cannot exceed 100 characters.")]
    public string SystemName { get; set; } = null!;

    /// <summary>User-editable label, even on system roles.</summary>
    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(100, ErrorMessage = "Display name cannot exceed 100 characters.")]
    public string DisplayName { get; set; } = null!;

    [MaxLength(300, ErrorMessage = "Description cannot exceed 300 characters.")]
    public string? Description { get; set; }

    /// <summary>
    /// The one app this role belongs to (TK-42). Owner of RetailErp and Owner of
    /// Payroll are different rows. A role may hold only permissions whose
    /// <c>Permission.Apps</c> include this app.
    /// </summary>
    public App App { get; set; } = App.RetailErp;

    public bool IsSystemRole { get; set; }

    public bool IsActive { get; set; } = true;
}

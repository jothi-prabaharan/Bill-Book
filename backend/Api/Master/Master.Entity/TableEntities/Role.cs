using System.ComponentModel.DataAnnotations;
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

    public bool IsSystemRole { get; set; }

    public bool IsActive { get; set; } = true;
}

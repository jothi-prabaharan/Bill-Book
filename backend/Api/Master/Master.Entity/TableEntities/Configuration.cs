using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// Generic key-value settings. A system-default row (OrgId null) ships the
/// value; a per-org row overrides it. Effective value = org row if present,
/// else system default.
/// </summary>
public class Configuration : AuditableEntity
{
    public Guid ConfigId { get; set; }

    /// <summary>Null = system default. Set = this org's override.</summary>
    public Guid? OrgId { get; set; }

    /// <summary>Stable key, e.g. unitPrice.decimals. Never renamed — code reads it.</summary>
    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(300, ErrorMessage = "Description cannot exceed 300 characters.")]
    public string? Description { get; set; }

    public ConfigDataType DataType { get; set; }

    /// <summary>Stored as text, parsed per DataType.</summary>
    [Required(ErrorMessage = "Value is required.")]
    [MaxLength(1000, ErrorMessage = "Value cannot exceed 1000 characters.")]
    public string Value { get; set; } = null!;

    [MaxLength(50, ErrorMessage = "Category cannot exceed 50 characters.")]
    public string? Category { get; set; }

    /// <summary>System keys can be overridden but not deleted.</summary>
    public bool IsSystem { get; set; }

}

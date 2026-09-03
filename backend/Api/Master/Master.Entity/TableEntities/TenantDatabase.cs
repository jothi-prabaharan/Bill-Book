using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// Tracks database shards and their capacity (Elite vs Pro).
/// </summary>
public class TenantDatabase : AuditableEntity
{
    [Key]
    [MaxLength(50, ErrorMessage = "Database name cannot exceed 50 characters.")]
    public string DatabaseName { get; set; } = null!;

    [Required(ErrorMessage = "Plan type is required.")]
    [MaxLength(20, ErrorMessage = "Plan type cannot exceed 20 characters.")]
    public string PlanType { get; set; } = "Pro";

    public int MaxOrganizations { get; set; }

    public int CurrentOrganizations { get; set; }
}

using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
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

    public PlanTier PlanType { get; set; } = PlanTier.Pro;

    public int MaxOrganizations { get; set; }

    public int CurrentOrganizations { get; set; }
}

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

    /// <summary>
    /// How many customers this database holds at most (TK-46): 100 for a pooled
    /// shard, 1 for an Elite customer's own. Capacity is counted in customers,
    /// not branches, because a customer's branches always share its database.
    /// </summary>
    public int MaxCustomers { get; set; } = 100;

    /// <summary>
    /// How many customers it holds now. Claimed with a guarded update at signup,
    /// and recounted from <c>mst.Customers</c> at every start.
    /// </summary>
    public int CurrentCustomers { get; set; }
}

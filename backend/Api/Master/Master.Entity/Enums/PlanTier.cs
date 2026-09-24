namespace Master.Entity.Enums;

/// <summary>
/// A customer's plan, and the plan a tenant database (shard) is kept for
/// (H0.1, TK-42). It replaces the free strings on <c>Customer.PlanTier</c> and
/// <c>TenantDatabase.PlanType</c>. Stored by name, so the column's values did not
/// change.
/// </summary>
public enum PlanTier
{
    Trial = 1,

    Standard = 2,

    Pro = 3,

    /// <summary>A customer with a database of its own (a shard of capacity one).</summary>
    Elite = 4,
}

namespace Shared.Kernel.Tenancy;

/// <summary>
/// A customer's plan tier (TK-42, TK-129).
/// Stored by name in Postgres so the column values are unchanged.
/// Basic is an alias for Standard; call support requires Pro or Elite.
/// </summary>
public enum PlanTier
{
    Trial = 1,

    Standard = 2,

    Basic = Standard,

    Pro = 3,

    Elite = 4,
}

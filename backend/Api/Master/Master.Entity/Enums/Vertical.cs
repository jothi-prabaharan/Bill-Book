namespace Master.Entity.Enums;

/// <summary>
/// The trade a branch is in — the vertical that narrows what it is seeded with,
/// what a new item defaults to, and which settings screens apply.
///
/// Superseded by <see cref="Shared.Kernel.Apps.BusinessType"/> (D-36, TK-126).
/// Kept for backward compatibility until Master.Entity drops Organization.Vertical in TK-127.
/// </summary>
public enum Vertical
{
    /// <summary>Everything. The default for a branch that declares nothing.</summary>
    General = 1,

    Pharma = 2,

    Jewellery = 3,

    /// <summary>School/educational institution (aligned with BusinessType.School).</summary>
    School = 4,

    Service = 5,

    Restaurant = 6,
}

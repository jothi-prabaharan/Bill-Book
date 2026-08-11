namespace Inventory.Entity.Enums;

/// <summary>
/// How the quantities on a sheet were arrived at.
///
/// Both end as a movement of the difference, so this changes what a person types
/// rather than what is posted. It is stored anyway, because the two are not the
/// same claim: a count says "there are eleven of these on the shelf", while an
/// adjustment says "write two off". Six months later, only the first can be
/// checked against the shelf, and only the first explains why the difference was
/// what it was.
/// </summary>
public enum StockAdjustmentKind
{
    /// <summary>
    /// The difference is keyed directly — write off two, add one back. Used for
    /// a breakage or a write-off, where nobody counted the whole item.
    /// </summary>
    Adjustment = 0,

    /// <summary>
    /// Counted totals are keyed and the difference is worked out. The quantity
    /// on hand at the moment of counting is snapshotted on each line, so the
    /// arithmetic can be re-checked later against what the system believed at
    /// the time rather than what it believes now.
    /// </summary>
    PhysicalCount = 1,
}

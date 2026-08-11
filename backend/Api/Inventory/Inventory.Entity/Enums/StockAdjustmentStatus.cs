namespace Inventory.Entity.Enums;

/// <summary>
/// The life of a stock adjustment.
///
/// <b>There is no Void, and that is the difference from a money document.</b>
/// Voiding a payment withdraws its ledger rows and nothing else has happened;
/// voiding a stock adjustment would have to un-move stock that has physically
/// moved. The movements behind a posted adjustment are append-only — a mistake
/// is corrected by a movement in the opposite direction, never by deleting the
/// first — so the correction is a second document, and
/// <see cref="Reversed"/> records that it happened.
/// </summary>
public enum StockAdjustmentStatus
{
    /// <summary>
    /// Being keyed. It holds no number, has moved no stock and touched no
    /// ledger, and can be edited or deleted freely.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Stock has moved and a number is taken. Immutable from here: the movements
    /// it wrote are already in the item's history.
    /// </summary>
    Posted = 1,

    /// <summary>
    /// Posted, and later undone by a mirror-image adjustment. Both documents
    /// stay and both keep their numbers — the pair is the audit trail, and a
    /// series with a hole in it is what an auditor asks about.
    /// </summary>
    Reversed = 2,
}

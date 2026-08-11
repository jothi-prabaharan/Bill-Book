namespace Inventory.Entity.Enums;

/// <summary>
/// Why stock was adjusted.
///
/// An enum rather than a reason master, deliberately. The list is short, it is
/// the same list in every branch and every trade, and it is what a shrinkage
/// report groups by — a per-branch master would let two branches spell the same
/// reason differently and make that report meaningless across a customer. If a
/// customer ever needs their own wording, <see cref="StockAdjustment.Notes"/> is
/// already there for it.
///
/// The value matters more than it looks: this is the only thing that
/// distinguishes stock that was stolen from stock that was counted wrong, and a
/// business that cannot tell those apart cannot act on either.
/// </summary>
public enum StockAdjustmentReason
{
    /// <summary>The count found a different quantity, and nobody knows why.</summary>
    CountCorrection = 0,

    /// <summary>Broken, spoiled, or otherwise destroyed.</summary>
    Damage = 1,

    /// <summary>Past its expiry and written off.</summary>
    Expired = 2,

    /// <summary>Missing, with theft suspected.</summary>
    Theft = 3,

    /// <summary>Given away — a sample, a donation, a replacement under warranty.</summary>
    Sample = 4,

    /// <summary>Consumed internally rather than sold.</summary>
    InternalUse = 5,

    /// <summary>Found: stock that was there all along and was not on the books.</summary>
    FoundStock = 6,

    /// <summary>The mirror written to undo an earlier adjustment.</summary>
    Reversal = 7,

    /// <summary>None of the above. <see cref="StockAdjustment.Notes"/> carries the explanation.</summary>
    Other = 99,
}

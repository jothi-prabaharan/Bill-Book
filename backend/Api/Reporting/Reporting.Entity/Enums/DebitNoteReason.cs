namespace Reporting.Entity.Enums;

/// <summary>
/// Why a debit note was raised against a bill, as Purchase stores it (by name). Only a
/// <see cref="PurchaseReturn"/> sends goods back; the others change what was charged and leave the
/// units bought as they were.
/// </summary>
public enum DebitNoteReason
{
    /// <summary>Goods went back to the vendor. The only reason that moves stock.</summary>
    PurchaseReturn = 0,

    /// <summary>The vendor billed the wrong price. Nothing physical happened.</summary>
    PriceCorrection = 1,

    /// <summary>A discount agreed after the bill was raised.</summary>
    PostPurchaseDiscount = 2,

    /// <summary>Short delivery or damage in transit. No stock goes back.</summary>
    Deficiency = 3,

    /// <summary>The bill should not have existed and is being reversed in full.</summary>
    Cancellation = 4,
}

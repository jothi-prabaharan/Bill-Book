namespace Purchase.Entity.Enums;

/// <summary>
/// How far a purchase order has been received.
///
/// <b>Deliberately not <c>Sales.Entity.Enums.FulfilmentStatus</c>, and not
/// shared with it.</b> The two read the same shape and mean different events:
/// a sales order is partly <i>delivered</i>, a purchase order is partly
/// <i>received</i>. Both are stored as strings, so one shared enum would force
/// one vocabulary onto both schemas and leave <c>pur.PurchaseOrders</c> holding
/// the word "PartlyDelivered" about goods arriving. The base classes are shared
/// because their columns are genuinely the same column; this is not.
/// </summary>
public enum PurchaseFulfilmentStatus
{
    /// <summary>Nothing received yet.</summary>
    Open = 0,

    /// <summary>Some lines, or some quantity, have arrived.</summary>
    PartlyReceived = 1,

    /// <summary>Nothing further is coming — either fully received, or closed short deliberately.</summary>
    Closed = 2,

    /// <summary>The order was withdrawn before it arrived.</summary>
    Cancelled = 3,
}

/// <summary>
/// Why a debit note was raised against a bill.
///
/// The mirror of <c>CreditNoteReason</c>, and on the document for the same
/// reason: GSTR-2 asks what is being corrected, and only
/// <see cref="PurchaseReturn"/> moves stock.
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

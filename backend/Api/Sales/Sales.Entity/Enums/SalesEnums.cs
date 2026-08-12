namespace Sales.Entity.Enums;

/// <summary>
/// How far a sales order has been delivered.
///
/// Its own column rather than something derived from the challans beneath it: an
/// order can be closed short by agreement, and "everything delivered" and
/// "nothing more coming" are different facts that would otherwise be the same
/// arithmetic.
/// </summary>
public enum FulfilmentStatus
{
    /// <summary>Nothing delivered yet.</summary>
    Open = 0,

    /// <summary>Some lines, or some quantity, have gone out.</summary>
    PartlyDelivered = 1,

    /// <summary>Nothing further is coming — either fully delivered, or closed short deliberately.</summary>
    Closed = 2,

    /// <summary>The customer withdrew. Any reservation is released.</summary>
    Cancelled = 3,
}

/// <summary>
/// Why goods are leaving on a delivery challan, which decides what it posts.
///
/// <b>Only <see cref="Sale"/> is a supply.</b> The other four move goods without
/// selling them, so they reach no ledger at all — stock moves, ownership does
/// not. Getting this wrong books revenue on a sample.
/// </summary>
public enum ChallanType
{
    /// <summary>Goods sold, invoice to follow. The only type that posts.</summary>
    Sale = 0,

    /// <summary>Out to a job worker and coming back. Still the branch's stock.</summary>
    JobWork = 1,

    /// <summary>Sent on approval. A sale only if the customer keeps them.</summary>
    Approval = 2,

    /// <summary>To another branch. A branch is a separate set of books, so this is not a sale.</summary>
    BranchTransfer = 3,

    /// <summary>A sample. Leaves stock, earns nothing.</summary>
    Sample = 4,
}

/// <summary>
/// Why a credit note was raised.
///
/// It is on the document because GSTR-1 asks for it — a credit note against a
/// filed invoice has to say what it is correcting — not because a report wanted
/// a grouping.
/// </summary>
public enum CreditNoteReason
{
    /// <summary>Goods came back. The only reason that moves stock.</summary>
    SalesReturn = 0,

    /// <summary>The invoice was priced wrong. Nothing physical happened.</summary>
    PriceCorrection = 1,

    /// <summary>A discount agreed after the invoice was raised.</summary>
    PostSaleDiscount = 2,

    /// <summary>Short delivery, damage, a gesture. No stock comes back.</summary>
    Deficiency = 3,

    /// <summary>The invoice should not have existed and is being cancelled in full.</summary>
    Cancellation = 4,
}

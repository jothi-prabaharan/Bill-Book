namespace Reporting.Entity.Enums;

/// <summary>
/// Where a sales invoice stands in its life on the Invoice Track Report (TK-226), as at the
/// report's last date: the document lifecycle (<c>Draft</c>, <c>ReadyToPost</c>, posted, void)
/// with a posted invoice split by what has been settled, the Receivable Invoice Summary's way.
/// <b>Overdue wins over partly paid.</b>
/// </summary>
public enum InvoiceTrackStatus
{
    /// <summary>Being keyed; not in the books.</summary>
    Draft = 1,

    /// <summary>Complete and checked, waiting to post; not in the books.</summary>
    ReadyToPost = 2,

    /// <summary>Posted, nothing received, and not past its due date (or no due date).</summary>
    AwaitingPayment = 3,

    /// <summary>Something received, something still owed, and not past its due date.</summary>
    PartlyPaid = 4,

    /// <summary>Past its due date on the last day with something still owed.</summary>
    Overdue = 5,

    /// <summary>Nothing left owed.</summary>
    Paid = 6,

    /// <summary>Voided: listed only when asked for.</summary>
    Void = 7,
}

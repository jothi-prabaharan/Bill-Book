namespace Reporting.Entity.Enums;

/// <summary>
/// Where a vendor bill stands in its life on the Bills Track Report (TK-237), as at the report's
/// last date: the document lifecycle (<c>Draft</c>, <c>ReadyToPost</c>, posted, void) with a posted
/// bill split by what has been paid, the Payable Invoice Summary's way. <b>Overdue wins over partly
/// paid.</b> Whether the bill went through an approval chain is a separate fact, carried beside the
/// status rather than as one of them.
/// </summary>
public enum BillTrackStatus
{
    /// <summary>Being keyed; not in the books.</summary>
    Draft = 1,

    /// <summary>Complete, checked and, where a workflow applies, approved; waiting to post. Not in the books.</summary>
    ReadyToPost = 2,

    /// <summary>Posted, nothing paid, and not past its due date.</summary>
    AwaitingPayment = 3,

    /// <summary>Something paid, something still owed, and not past its due date.</summary>
    PartlyPaid = 4,

    /// <summary>Past its due date on the last day with something still owed.</summary>
    Overdue = 5,

    /// <summary>Nothing left owed to the vendor.</summary>
    Paid = 6,

    /// <summary>Voided: listed only when asked for.</summary>
    Void = 7,
}

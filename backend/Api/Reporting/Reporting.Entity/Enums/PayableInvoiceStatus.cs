namespace Reporting.Entity.Enums;

/// <summary>
/// Where a posted vendor bill, or debit note, stands on the Payable Invoice reports (TK-235), as at the report's last
/// date. Drafts are never listed. <b>Overdue wins over partly paid</b>: a bill past its due date with something still
/// owed is overdue whether or not part of it has been paid.
/// </summary>
public enum PayableInvoiceStatus
{
    /// <summary>Posted, nothing paid, and not past its due date.</summary>
    AwaitingPayment = 1,

    /// <summary>Something paid, something still owed, and not past its due date.</summary>
    PartlyPaid = 2,

    /// <summary>Past its due date on the last day with something still owed.</summary>
    Overdue = 3,

    /// <summary>Nothing left owed.</summary>
    Paid = 4,

    /// <summary>Voided: it settles nothing and is listed only when asked for.</summary>
    Void = 5,
}

namespace Reporting.Entity.Enums;

/// <summary>
/// Where a posted sales invoice, or credit note, stands on the Receivable Invoice reports
/// (TK-224), as at the report's last date. Drafts are never listed. <b>Overdue wins over
/// partly paid</b>: an invoice past its due date with something still owed is overdue
/// whether or not part of it has been received.
/// </summary>
public enum ReceivableInvoiceStatus
{
    /// <summary>Posted, nothing received, and not past its due date (or no due date).</summary>
    AwaitingPayment = 1,

    /// <summary>Something received, something still owed, and not past its due date.</summary>
    PartlyPaid = 2,

    /// <summary>Past its due date on the last day with something still owed.</summary>
    Overdue = 3,

    /// <summary>Nothing left owed.</summary>
    Paid = 4,

    /// <summary>Voided: it settles nothing and is listed only when asked for.</summary>
    Void = 5,
}

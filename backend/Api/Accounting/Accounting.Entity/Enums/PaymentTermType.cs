namespace Accounting.Entity.Enums;

/// <summary>
/// How a due date is worked out from a document date. The type decides which of
/// DueDays and DueDayOfMonth means anything, which is why they are not one column.
/// </summary>
public enum PaymentTermType
{
    /// <summary>Due the day the document is dated.</summary>
    DueOnReceipt = 0,

    /// <summary>Document date plus DueDays.</summary>
    Net = 1,

    /// <summary>Last day of the document's month, then plus DueDays.</summary>
    EndOfMonth = 2,

    /// <summary>DueDayOfMonth of the following month, clamped to that month's length.</summary>
    DayOfNextMonth = 3,
}

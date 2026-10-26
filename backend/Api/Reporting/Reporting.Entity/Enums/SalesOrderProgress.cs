namespace Reporting.Entity.Enums;

/// <summary>
/// How far a sales order has got on the Sales Order Track Report (TK-230), worked out from what its
/// lines have had delivered and invoiced, as Sales does it: <b>every line</b> counts, so one line
/// that cannot be delivered, or billed, keeps the order from being delivered, or invoiced.
/// </summary>
public enum SalesOrderProgress
{
    /// <summary>Nothing has been delivered, and not every line is billed.</summary>
    Open = 1,

    /// <summary>Something has been delivered, but not every line in full.</summary>
    PartlyDelivered = 2,

    /// <summary>Every line is delivered in full, and not every line is billed.</summary>
    Delivered = 3,

    /// <summary>Every line is billed in full.</summary>
    Invoiced = 4,

    /// <summary>Closed with something left undelivered, and not every line billed: nothing further is coming.</summary>
    ClosedShort = 5,

    /// <summary>Cancelled by the customer, or voided: nothing is expected of it.</summary>
    Cancelled = 6,
}

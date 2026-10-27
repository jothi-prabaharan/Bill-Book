namespace Reporting.Entity.Enums;

/// <summary>
/// How far a purchase order has got on the Purchase Order Track Report (TK-240), worked out from what
/// its lines have had received and billed: <b>every line</b> counts, so one line that has not arrived,
/// or not been billed, keeps the order from being received, or billed.
/// </summary>
public enum PurchaseOrderProgress
{
    /// <summary>Nothing has been received, and not every line is billed.</summary>
    Open = 1,

    /// <summary>Something has been received, but not every line in full.</summary>
    PartlyReceived = 2,

    /// <summary>Every line is received in full, and not every line is billed.</summary>
    Received = 3,

    /// <summary>Every line is billed in full.</summary>
    Billed = 4,

    /// <summary>Closed with something never received, and not every line billed: nothing further is coming.</summary>
    ClosedShort = 5,

    /// <summary>Withdrawn before it arrived, or voided: nothing is expected of it.</summary>
    Cancelled = 6,
}

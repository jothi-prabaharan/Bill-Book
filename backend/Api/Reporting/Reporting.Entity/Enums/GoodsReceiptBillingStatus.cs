namespace Reporting.Entity.Enums;

/// <summary>
/// Where a goods receipt stands with its bills on the Receive Order Track Report (TK-242), worked out
/// from what its lines have had billed against the quantity accepted: <b>every line</b> counts, so one
/// line not yet billed in full keeps the receipt from being billed.
/// </summary>
public enum GoodsReceiptBillingStatus
{
    /// <summary>Nothing has been billed, and something was accepted: all of it is still to bill.</summary>
    NotBilled = 1,

    /// <summary>Something has been billed, but not every line in full.</summary>
    PartlyBilled = 2,

    /// <summary>Every line with goods accepted is billed in full.</summary>
    Billed = 3,

    /// <summary>Every line was rejected, so nothing was taken into stock and nothing is owed.</summary>
    NothingToBill = 4,
}

namespace Reporting.Entity.Enums;

/// <summary>
/// How far a delivery challan has been billed, for the Delivery Order Track Report (TK-232), worked out
/// from every line: a line is billed when the invoices raised from the challan cover its quantity.
/// </summary>
public enum ChallanProgress
{
    /// <summary>Nothing on it has been billed.</summary>
    NotInvoiced = 1,

    /// <summary>Something has been billed and something is still to bill.</summary>
    PartlyInvoiced = 2,

    /// <summary>Every line has been billed in full.</summary>
    Invoiced = 3,

    /// <summary>Withdrawn: a challan can be voided only as a draft, so it delivered nothing.</summary>
    Void = 4,
}

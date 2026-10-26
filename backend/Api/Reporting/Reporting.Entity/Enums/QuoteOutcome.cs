namespace Reporting.Entity.Enums;

/// <summary>
/// Where a quote stands on the Quotation Track Report (TK-231), as at the report's last date. A quote
/// that has been turned into an order or an invoice is converted whatever else it said; one the customer
/// declined or accepted is that; one past its valid-until date with no answer is expired.
/// </summary>
public enum QuoteOutcome
{
    /// <summary>Raised, not answered, not converted, and still valid.</summary>
    Open = 1,

    /// <summary>Past its valid-until date with no answer, and not converted.</summary>
    Expired = 2,

    /// <summary>The customer accepted it on the portal, and it has not been converted yet.</summary>
    Accepted = 3,

    /// <summary>A sales order has been raised from it, and no invoice.</summary>
    ConvertedToOrder = 4,

    /// <summary>An invoice has been raised from it.</summary>
    ConvertedToInvoice = 5,

    /// <summary>The customer declined it on the portal, and it was not converted.</summary>
    Declined = 6,

    /// <summary>Voided: counted, but not a quote that was raised.</summary>
    Void = 7,
}

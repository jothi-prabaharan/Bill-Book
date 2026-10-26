using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Quotation Track Report (TK-231): each quote's outcome (open, expired, accepted, converted to an
// order or an invoice, or declined) and the conversion rate over a period. Benchmarked on AutoCount's
// Quotation Tracking, Odoo's quotations with conversion and Zoho's quote status. Values are in the
// branch's base currency.

/// <summary>
/// What the report is asked for: the quote-date period, both dates required (D-67), optionally one
/// customer and one outcome, a page of quotes at a time. A quote is expired when it is past its valid-until
/// date on the To date. The branch is always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class QuotationTrackReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only quotes with this outcome. Blank lists every outcome but void, which is listed only when asked for.</summary>
    public QuoteOutcome? Outcome { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>The order or invoice a quote was turned into.</summary>
public sealed class QuoteConversionDto
{
    /// <summary>SOR for a sales order, INV for an invoice.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }
}

/// <summary>One quote with its outcome and what it was converted to.</summary>
public sealed class QuotationTrackRowDto
{
    public long QuoteId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    /// <summary>The last day the customer can take it up.</summary>
    public DateOnly ValidUntil { get; set; }

    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The quote's total in its own currency.</summary>
    public decimal TotalInCurrency { get; set; }

    /// <summary>The same total in base currency.</summary>
    public decimal Total { get; set; }

    public QuoteOutcome Outcome { get; set; }

    /// <summary>The invoice, else the earliest order, raised from it; null when it was not converted.</summary>
    public QuoteConversionDto? ConvertedTo { get; set; }

    /// <summary>Days from the quote to the document it was converted into; null when it was not converted.</summary>
    public int? DaysToConvert { get; set; }
}

/// <summary>The quotes raised in the period and how many became orders or invoices, by count and by value (base currency).</summary>
public sealed class QuotationTrackSummaryDto
{
    /// <summary>Quotes raised: every outcome but void.</summary>
    public int Raised { get; set; }

    public decimal RaisedValue { get; set; }

    /// <summary>Quotes turned into an order or an invoice.</summary>
    public int Converted { get; set; }

    public decimal ConvertedValue { get; set; }

    /// <summary>Converted over raised, as a percentage; null when none were raised.</summary>
    public decimal? ConversionRateByCount { get; set; }

    /// <summary>Converted value over raised value, as a percentage; null when none were raised.</summary>
    public decimal? ConversionRateByValue { get; set; }
}

public sealed class QuotationTrackReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of quotes, newest first.</summary>
    public List<QuotationTrackRowDto> Quotes { get; set; } = [];

    /// <summary>How many quotes the filters match, all pages.</summary>
    public int TotalQuotes { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many quotes have each outcome, for the tabs: every outcome is present, none included. Void is counted but is not part of All.</summary>
    public Dictionary<QuoteOutcome, int> OutcomeCounts { get; set; } = [];

    /// <summary>The quotes raised in the period (every status the other filters leave) and the conversion rate, not only the page or the outcome chosen.</summary>
    public QuotationTrackSummaryDto Summary { get; set; } = new();

    /// <summary>The value of every quote the filters match, in base currency.</summary>
    public decimal TotalValue { get; set; }
}

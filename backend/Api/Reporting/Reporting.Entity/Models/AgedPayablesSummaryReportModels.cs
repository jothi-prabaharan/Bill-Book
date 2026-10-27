using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Aged Payables Summary (TK-233): what the branch owed each vendor on a date and how overdue it was, in
// five columns: the Aged Receivables Summary the other way round, from the same open-document rule. Benchmarked
// on Xero's Aged Payables Summary, AutoCount's Creditor Aging and Odoo's Aged Payable. Every figure is in the
// branch's base currency.

/// <summary>
/// What the report is asked for: the date the balances are taken at, required (D-67); what a document's age
/// counts from and how wide each overdue column is; and one vendor, when wanted. The branch is always the
/// token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class AgedPayablesSummaryReportRequest
{
    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsAtDate { get; set; }

    /// <summary>Due date when blank; the invoice-date value ages a bill from its bill date.</summary>
    public AgeBasis? AgeBy { get; set; }

    /// <summary>The width of each overdue column in days, 7, 15 or 30; 30 when blank.</summary>
    public int? BucketDays { get; set; }

    /// <summary>Only this vendor; every vendor when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Also list vendors with nothing owed.</summary>
    public bool ShowZeroBalances { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>
/// One vendor's balance, spread across the five columns. A debit note, or a payment or advance not yet applied
/// to a bill, is a negative amount in <see cref="Current"/>, so <see cref="Total"/> is what the branch owes the vendor.
/// </summary>
public sealed class AgedPayablesSummaryRowDto
{
    /// <summary>Null on the row for what no vendor claims, such as a manual journal to Accounts Payable with no contact.</summary>
    public long? ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    /// <summary>The vendor is a micro or small enterprise, whose bills the Sec 43B(h) 45-day rule applies to.</summary>
    public bool IsMsme { get; set; }

    /// <summary>Not yet due, less debit notes and unapplied payments.</summary>
    public decimal Current { get; set; }

    /// <summary>Overdue by 1 to one bucket's width.</summary>
    public decimal Bucket1 { get; set; }

    public decimal Bucket2 { get; set; }

    public decimal Bucket3 { get; set; }

    /// <summary>Overdue by more than three buckets.</summary>
    public decimal Bucket4 { get; set; }

    public decimal Total { get; set; }
}

/// <summary>The vendors, with the totals of all of them.</summary>
public sealed class AgedPayablesSummaryReportResultDto
{
    public DateOnly AsAtDate { get; set; }

    public AgeBasis AgeBy { get; set; }

    public int BucketDays { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>The five column headings, Current first, for the bucket width in use.</summary>
    public List<string> BucketLabels { get; set; } = [];

    public List<AgedPayablesSummaryRowDto> Vendors { get; set; } = [];

    /// <summary>Every column added up: the Accounts Payable balance on the date.</summary>
    public AgedPayablesSummaryRowDto Totals { get; set; } = new();
}

/// <summary>A vendor the filter can pick.</summary>
public sealed class AgedPayablesVendorChoiceDto
{
    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;
}

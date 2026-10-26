using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Aged Receivables Summary (TK-222): what each customer owed on a date and how
// overdue it was, in five columns. Benchmarked on Xero's Aged Receivables Summary
// (as at, ageing by due date or invoice date, bucket size), AutoCount's Debtor
// Aging and Odoo's Aged Receivable. Every figure is in the branch's base currency.

/// <summary>
/// What the report is asked for: the date the balances are taken at, required (D-67);
/// what a document's age counts from and how wide each overdue column is; and one
/// customer, when wanted. The branch is always the token's: a request naming another
/// branch is refused with 403.
/// </summary>
public sealed class AgedReceivablesSummaryReportRequest
{
    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsAtDate { get; set; }

    /// <summary>Due date when blank.</summary>
    public AgeBasis? AgeBy { get; set; }

    /// <summary>The width of each overdue column in days, 7, 15 or 30; 30 when blank.</summary>
    public int? BucketDays { get; set; }

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Also list customers with nothing owed.</summary>
    public bool ShowZeroBalances { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>
/// One customer's balance, spread across the five columns. A credit note, or a receipt
/// or advance not yet applied to an invoice, is a negative amount in <see cref="Current"/>,
/// so <see cref="Total"/> is the customer's receivable balance.
/// </summary>
public sealed class AgedReceivablesSummaryRowDto
{
    /// <summary>Null on the row for what no customer claims, such as a manual journal to Accounts Receivable with no contact.</summary>
    public long? ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    /// <summary>Not yet due, less credits and unapplied receipts.</summary>
    public decimal Current { get; set; }

    /// <summary>Overdue by 1 to one bucket's width.</summary>
    public decimal Bucket1 { get; set; }

    public decimal Bucket2 { get; set; }

    public decimal Bucket3 { get; set; }

    /// <summary>Overdue by more than three buckets.</summary>
    public decimal Bucket4 { get; set; }

    public decimal Total { get; set; }
}

/// <summary>The customers, with the totals of all of them.</summary>
public sealed class AgedReceivablesSummaryReportResultDto
{
    public DateOnly AsAtDate { get; set; }

    public AgeBasis AgeBy { get; set; }

    public int BucketDays { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>The five column headings, Current first, for the bucket width in use.</summary>
    public List<string> BucketLabels { get; set; } = [];

    public List<AgedReceivablesSummaryRowDto> Customers { get; set; } = [];

    /// <summary>Every column added up: the Accounts Receivable balance on the date.</summary>
    public AgedReceivablesSummaryRowDto Totals { get; set; } = new();
}

/// <summary>A customer the filter can pick.</summary>
public sealed class AgedReceivablesCustomerChoiceDto
{
    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Aged Receivables Detail (TK-223): the Aged Receivables Summary one document at a
// time, what makes up each customer's balance. Benchmarked on Xero's Aged Receivables
// Detail, AutoCount's Debtor Aging Detail and Odoo's Aged Receivable expanded by partner.
// Every figure is in the branch's base currency, and a customer's subtotal is the figure
// the summary gives for that customer, because both read the same open documents.

/// <summary>
/// What the report is asked for: the date the balances are taken at, required (D-67),
/// what a document's age counts from, how wide each overdue column is, and one customer
/// when wanted: the summary's filters. The branch is always the token's: a request naming
/// another branch is refused with 403.
/// </summary>
public sealed class AgedReceivablesDetailReportRequest
{
    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsAtDate { get; set; }

    /// <summary>Due date when blank.</summary>
    public AgeBasis? AgeBy { get; set; }

    /// <summary>The width of each overdue column in days, 7, 15 or 30; 30 when blank.</summary>
    public int? BucketDays { get; set; }

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>
/// One document that is still open on the date. <see cref="Outstanding"/> is what is left,
/// positive when it is owed and negative for a credit note, receipt or advance not yet
/// applied; <see cref="Original"/> is what it came to before anything was applied, signed the
/// same way, and <see cref="Settled"/> the difference. The outstanding amount sits in the one
/// column its age gives.
/// </summary>
public sealed class AgedReceivablesDetailDocumentDto
{
    public ReceivableDocumentKind Kind { get; set; }

    /// <summary>The three-letter code its postings carry, such as INV, CRN or RCM.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    [MaxLength(50, ErrorMessage = "Document number cannot exceed 50 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    /// <summary>Only an invoice has a due date; blank on anything else.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>How many days past the date it is counted from the as-at date is; blank for a credit and for what is not yet due.</summary>
    public int? DaysOverdue { get; set; }

    public decimal Original { get; set; }

    public decimal Settled { get; set; }

    public decimal Outstanding { get; set; }

    /// <summary>Not yet due, or a credit.</summary>
    public decimal Current { get; set; }

    public decimal Bucket1 { get; set; }

    public decimal Bucket2 { get; set; }

    public decimal Bucket3 { get; set; }

    public decimal Bucket4 { get; set; }
}

/// <summary>The figures of a customer's documents, or of all of them, added up.</summary>
public sealed class AgedReceivablesDetailTotalsDto
{
    public decimal Original { get; set; }

    public decimal Settled { get; set; }

    public decimal Outstanding { get; set; }

    public decimal Current { get; set; }

    public decimal Bucket1 { get; set; }

    public decimal Bucket2 { get; set; }

    public decimal Bucket3 { get; set; }

    public decimal Bucket4 { get; set; }
}

/// <summary>A customer's open documents, oldest first, and what they come to.</summary>
public sealed class AgedReceivablesDetailCustomerDto
{
    /// <summary>Null for what no customer claims, such as a manual journal to Accounts Receivable with no contact.</summary>
    public long? ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    public List<AgedReceivablesDetailDocumentDto> Documents { get; set; } = [];

    public AgedReceivablesDetailTotalsDto Subtotal { get; set; } = new();
}

public sealed class AgedReceivablesDetailReportResultDto
{
    public DateOnly AsAtDate { get; set; }

    public AgeBasis AgeBy { get; set; }

    public int BucketDays { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>The five column headings, Current first, for the bucket width in use.</summary>
    public List<string> BucketLabels { get; set; } = [];

    public List<AgedReceivablesDetailCustomerDto> Customers { get; set; } = [];

    /// <summary>Every document added up: the Accounts Receivable balance on the date.</summary>
    public AgedReceivablesDetailTotalsDto Totals { get; set; } = new();
}

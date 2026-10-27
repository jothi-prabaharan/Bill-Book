using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Aged Payables Details (TK-234): the Aged Payables Summary one document at a time, what makes up what the
// branch owes each vendor. Benchmarked on Xero's Aged Payables Detail and AutoCount's Creditor Aging Detail.
// Every figure is in the branch's base currency, and a vendor's subtotal is the figure the summary gives for that
// vendor, because both read the same open documents.

/// <summary>
/// What the report is asked for: the date the balances are taken at, required (D-67), what a document's age counts
/// from, how wide each overdue column is, and one vendor when wanted: the summary's filters. The branch is always the
/// token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class AgedPayablesDetailsReportRequest
{
    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsAtDate { get; set; }

    /// <summary>Due date when blank; the invoice-date value ages a bill from its bill date.</summary>
    public AgeBasis? AgeBy { get; set; }

    /// <summary>The width of each overdue column in days, 7, 15 or 30; 30 when blank.</summary>
    public int? BucketDays { get; set; }

    /// <summary>Only this vendor; every vendor when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>
/// One document that is still open on the date. <see cref="Outstanding"/> is what is left, positive when the branch owes
/// it and negative for a debit note, payment or advance not yet applied; <see cref="Original"/> is what it came to before
/// anything was applied, signed the same way, and <see cref="Settled"/> the difference. The outstanding amount sits in the
/// one column its age gives.
/// </summary>
public sealed class AgedPayablesDetailsDocumentDto
{
    public PayableDocumentKind Kind { get; set; }

    /// <summary>The three-letter code its postings carry, such as BIL, DBN or SPM.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    [MaxLength(50, ErrorMessage = "Document number cannot exceed 50 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    /// <summary>Only a bill has a due date; blank on anything else.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>How many days past the date it is counted from the as-at date is; blank for a debit and for what is not yet due.</summary>
    public int? DaysOverdue { get; set; }

    public decimal Original { get; set; }

    public decimal Settled { get; set; }

    public decimal Outstanding { get; set; }

    /// <summary>Not yet due, or a debit.</summary>
    public decimal Current { get; set; }

    public decimal Bucket1 { get; set; }

    public decimal Bucket2 { get; set; }

    public decimal Bucket3 { get; set; }

    public decimal Bucket4 { get; set; }
}

/// <summary>The figures of a vendor's documents, or of all of them, added up.</summary>
public sealed class AgedPayablesDetailsTotalsDto
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

/// <summary>A vendor's open documents, oldest first, and what they come to.</summary>
public sealed class AgedPayablesDetailsVendorDto
{
    /// <summary>Null for what no vendor claims, such as a manual journal to Accounts Payable with no contact.</summary>
    public long? ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    /// <summary>The vendor is a micro or small enterprise, whose bills the Sec 43B(h) 45-day rule applies to.</summary>
    public bool IsMsme { get; set; }

    public List<AgedPayablesDetailsDocumentDto> Documents { get; set; } = [];

    public AgedPayablesDetailsTotalsDto Subtotal { get; set; } = new();
}

public sealed class AgedPayablesDetailsReportResultDto
{
    public DateOnly AsAtDate { get; set; }

    public AgeBasis AgeBy { get; set; }

    public int BucketDays { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>The five column headings, Current first, for the bucket width in use.</summary>
    public List<string> BucketLabels { get; set; } = [];

    public List<AgedPayablesDetailsVendorDto> Vendors { get; set; } = [];

    /// <summary>Every document added up: the Accounts Payable balance on the date.</summary>
    public AgedPayablesDetailsTotalsDto Totals { get; set; } = new();
}

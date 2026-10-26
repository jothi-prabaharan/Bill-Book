using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Receivable Invoice Summary (TK-224): every sales invoice raised in a period with its
// taxable value, tax and total, what has been received and what is still due. Benchmarked on
// Xero's Receivable Invoice Summary, AutoCount's Sales Invoice Listing and Odoo's Invoices
// Analysis. Every amount is in the branch's base currency; a document in another currency
// also shows its own total. Received and due are as at the To date, so a report for a past
// period reads the same when it is run again.

/// <summary>
/// What the report is asked for: the period the invoices are dated in, both dates required
/// (D-67); optionally one customer, one currency, one status and credit notes as negatives.
/// A page of invoices is asked for at a time. The branch is always the token's: a request
/// naming another branch is refused with 403.
/// </summary>
public sealed class ReceivableInvoiceSummaryReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only documents in this currency; every currency when blank.</summary>
    [MaxLength(3, ErrorMessage = "Currency must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    /// <summary>Only this status. Blank lists every status but void, which is listed only when asked for.</summary>
    public ReceivableInvoiceStatus? Status { get; set; }

    /// <summary>Also list credit notes, as negative amounts.</summary>
    public bool IncludeCreditNotes { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 50;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>
/// One invoice, or credit note, with its figures in base currency. A credit note's amounts are
/// negative, so <see cref="Total"/> less <see cref="Received"/> is <see cref="Due"/> on every row:
/// for a credit note, received is what has been applied to invoices and due what is not yet.
/// </summary>
public sealed class ReceivableInvoiceSummaryRowDto
{
    /// <summary>INV or POS for an invoice, CRN for a credit note.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    /// <summary>Blank on a credit note and on an invoice with none.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>How many days past its due date it was on the To date; blank unless it is overdue.</summary>
    public int? DaysOverdue { get; set; }

    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The total in the document's own currency, the same as <see cref="Total"/> for one in the base currency.</summary>
    public decimal TotalInCurrency { get; set; }

    public decimal Taxable { get; set; }

    public decimal Cgst { get; set; }

    public decimal Sgst { get; set; }

    public decimal Igst { get; set; }

    public decimal Cess { get; set; }

    /// <summary>The four taxes added up.</summary>
    public decimal Tax { get; set; }

    public decimal Total { get; set; }

    public decimal Received { get; set; }

    public decimal Due { get; set; }

    public ReceivableInvoiceStatus Status { get; set; }
}

/// <summary>The figures of every invoice the filters match, added up, in base currency.</summary>
public sealed class ReceivableInvoiceSummaryTotalsDto
{
    public decimal Taxable { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }

    public decimal Received { get; set; }

    public decimal Due { get; set; }
}

public sealed class ReceivableInvoiceSummaryReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of invoices, newest first.</summary>
    public List<ReceivableInvoiceSummaryRowDto> Invoices { get; set; } = [];

    /// <summary>How many invoices the filters match, all pages.</summary>
    public int TotalInvoices { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many documents have each status, for the tabs: every status is present, none included. Void is counted but is not part of All.</summary>
    public Dictionary<ReceivableInvoiceStatus, int> StatusCounts { get; set; } = [];

    /// <summary>Added up over every invoice the filters match, not only this page.</summary>
    public ReceivableInvoiceSummaryTotalsDto Totals { get; set; } = new();
}

/// <summary>A currency the branch has invoiced in.</summary>
public sealed class ReceivableInvoiceCurrencyChoiceDto
{
    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;
}

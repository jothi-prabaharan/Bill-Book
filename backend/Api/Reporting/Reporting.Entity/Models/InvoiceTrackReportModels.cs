using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Invoice Track Report (TK-226): where each sales invoice is in its life, with the dates of
// each step and what is still due. Benchmarked on AutoCount's Invoice Tracking and Odoo's invoice
// follow-up view. It lists the invoices the Receivable Invoice Summary lists, with the drafts and
// the ready-to-post ones beside them, and amounts in the branch's base currency; a document in
// another currency also carries its own total.

/// <summary>
/// What the report is asked for: the invoice-date period, both dates required (D-67), optionally
/// one customer and one status, a page at a time. The branch is always the token's: a request
/// naming another branch is refused with 403.
/// </summary>
public sealed class InvoiceTrackReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only this status. Blank lists every status but void, which is listed only when asked for.</summary>
    public InvoiceTrackStatus? Status { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One invoice with the dates of its life, and what is owed on it as at the To date.</summary>
public sealed class InvoiceTrackRowDto
{
    /// <summary>INV, or POS for a sale rung up at the till.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    /// <summary>When it was posted; null for a draft and a ready-to-post invoice. A moment, so the page shows it in the viewer's own day.</summary>
    public DateTimeOffset? PostedAt { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>The latest settlement of it on or before the To date; null when nothing was settled.</summary>
    public DateOnly? LastPaymentDate { get; set; }

    /// <summary>How far past its due date it was on the To date; null unless it is overdue.</summary>
    public int? DaysOverdue { get; set; }

    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The total in the invoice's own currency.</summary>
    public decimal TotalInCurrency { get; set; }

    /// <summary>The total in base currency.</summary>
    public decimal Total { get; set; }

    /// <summary>What was received as at the To date; nought on a draft.</summary>
    public decimal Received { get; set; }

    /// <summary>What is still owed as at the To date; nought on a draft, which owes nothing yet.</summary>
    public decimal Due { get; set; }

    public InvoiceTrackStatus Status { get; set; }
}

/// <summary>The posted invoices the filters match added up, in base currency. A draft is not in the books and a voided invoice is withdrawn, so both are left out.</summary>
public sealed class InvoiceTrackTotalsDto
{
    public decimal Total { get; set; }

    public decimal Received { get; set; }

    public decimal Due { get; set; }
}

public sealed class InvoiceTrackReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of invoices, newest first.</summary>
    public List<InvoiceTrackRowDto> Invoices { get; set; } = [];

    /// <summary>How many invoices the filters match, all pages.</summary>
    public int TotalInvoices { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many invoices have each status, for the tabs: every status is present, none included. Void is counted but is not part of All.</summary>
    public Dictionary<InvoiceTrackStatus, int> StatusCounts { get; set; } = [];

    /// <summary>Added up over every posted invoice the filters match that still stands, not only this page.</summary>
    public InvoiceTrackTotalsDto Totals { get; set; } = new();
}

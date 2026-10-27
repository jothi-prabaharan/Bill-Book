using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Payable Invoice Summary (TK-235): every vendor bill dated in a period with its taxable value, tax and total, what
// has been paid and what is still due. The Receivable Invoice Summary the other way round. Benchmarked on Xero's
// Payable Invoice Summary, AutoCount's Purchase Invoice Listing and Odoo's Bills Analysis. Every amount is in the
// branch's base currency; a document in another currency also shows its own total. Paid and due are as at the To date,
// so a report for a past period reads the same when it is run again.

/// <summary>
/// What the report is asked for: the period the bills are dated in, both dates required (D-67); optionally one vendor, one
/// currency, one status and debit notes as negatives. A page of bills is asked for at a time. The branch is always the
/// token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class PayableInvoiceSummaryReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this vendor; every vendor when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only documents in this currency; every currency when blank.</summary>
    [MaxLength(3, ErrorMessage = "Currency must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    /// <summary>Only this status. Blank lists every status but void, which is listed only when asked for.</summary>
    public PayableInvoiceStatus? Status { get; set; }

    /// <summary>Also list debit notes, as negative amounts.</summary>
    public bool IncludeDebitNotes { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 50;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>
/// One bill, or debit note, with its figures in base currency. A debit note's amounts are negative. <see cref="Payable"/>
/// is what the vendor is owed on the bill: the total less the TDS withheld and, on a reverse-charge bill, the tax the
/// branch pays itself; so <see cref="Payable"/> less <see cref="Paid"/> is <see cref="Due"/> on every row, and is the
/// total less paid where there is neither. For a debit note, paid is what has been applied to bills and due what is not yet.
/// </summary>
public sealed class PayableInvoiceSummaryRowDto
{
    /// <summary>BIL for a bill, DBN for a debit note.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    /// <summary>The vendor's own number for the bill, which input tax credit is claimed on; blank on a debit note.</summary>
    [MaxLength(50, ErrorMessage = "Vendor bill number cannot exceed 50 characters.")]
    public string? VendorBillNo { get; set; }

    public DateOnly DocumentDate { get; set; }

    /// <summary>Blank on a debit note.</summary>
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

    /// <summary>The tax is paid by the branch itself, not to the vendor.</summary>
    public bool ReverseCharge { get; set; }

    /// <summary>TDS withheld from the vendor.</summary>
    public decimal Tds { get; set; }

    public decimal Total { get; set; }

    /// <summary>What the vendor is owed: the total less TDS and, on a reverse-charge bill, the tax.</summary>
    public decimal Payable { get; set; }

    public decimal Paid { get; set; }

    public decimal Due { get; set; }

    public PayableInvoiceStatus Status { get; set; }
}

/// <summary>The figures of every bill the filters match, added up, in base currency.</summary>
public sealed class PayableInvoiceSummaryTotalsDto
{
    public decimal Taxable { get; set; }

    public decimal Tax { get; set; }

    public decimal Tds { get; set; }

    public decimal Total { get; set; }

    public decimal Payable { get; set; }

    public decimal Paid { get; set; }

    public decimal Due { get; set; }
}

public sealed class PayableInvoiceSummaryReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of bills, newest first.</summary>
    public List<PayableInvoiceSummaryRowDto> Invoices { get; set; } = [];

    /// <summary>How many bills the filters match, all pages.</summary>
    public int TotalInvoices { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many documents have each status, for the tabs: every status is present, none included. Void is counted but is not part of All.</summary>
    public Dictionary<PayableInvoiceStatus, int> StatusCounts { get; set; } = [];

    /// <summary>Added up over every bill the filters match, not only this page.</summary>
    public PayableInvoiceSummaryTotalsDto Totals { get; set; } = new();
}

/// <summary>A currency the branch has been billed in.</summary>
public sealed class PayableInvoiceCurrencyChoiceDto
{
    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;
}

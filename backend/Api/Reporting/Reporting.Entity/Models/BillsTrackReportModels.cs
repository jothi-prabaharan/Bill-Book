using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;
using Shared.Kernel.Approvals;

namespace Reporting.Entity.Models;

// The Bills Track Report (TK-237): where each vendor bill is in its life, with the dates of each
// step, what is still owed to the vendor and, where the bill went through an approval chain, where
// that stands. Benchmarked on AutoCount's Purchase Invoice Tracking and Odoo's vendor bill
// follow-up. It lists the bills the Payable Invoice Summary lists, with the drafts and the
// ready-to-post ones beside them, and amounts in the branch's base currency; a bill in another
// currency also carries its own total.

/// <summary>
/// What the report is asked for: the bill-date period, both dates required (D-67), optionally one
/// vendor and one status, a page at a time. The branch is always the token's: a request naming
/// another branch is refused with 403.
/// </summary>
public sealed class BillsTrackReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this vendor; every vendor when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only this status. Blank lists every status but void, which is listed only when asked for.</summary>
    public BillTrackStatus? Status { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One bill with the dates of its life, what is owed on it as at the To date and where its approval stands.</summary>
public sealed class BillsTrackRowDto
{
    /// <summary>BIL.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    /// <summary>The vendor's own number for the bill.</summary>
    [MaxLength(50, ErrorMessage = "Vendor bill number cannot exceed 50 characters.")]
    public string? VendorBillNo { get; set; }

    public DateOnly DocumentDate { get; set; }

    /// <summary>When it was posted; null for a draft and a ready-to-post bill. A moment, so the page shows it in the viewer's own day.</summary>
    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>When it falls due; null for a bill not yet in the books, which has no due date until it is posted.</summary>
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

    /// <summary>The tax on the bill is the branch's own to pay, not the vendor's to be paid.</summary>
    public bool ReverseCharge { get; set; }

    /// <summary>The total in the bill's own currency.</summary>
    public decimal TotalInCurrency { get; set; }

    /// <summary>The total in base currency.</summary>
    public decimal Total { get; set; }

    /// <summary>What the vendor is owed on it: the total less TDS withheld and, on a reverse-charge bill, the tax. Nought on a draft.</summary>
    public decimal Payable { get; set; }

    /// <summary>What was paid as at the To date; nought on a draft.</summary>
    public decimal Paid { get; set; }

    /// <summary>What is still owed to the vendor as at the To date; nought on a draft, which owes nothing yet.</summary>
    public decimal Due { get; set; }

    public BillTrackStatus Status { get; set; }

    /// <summary>Where the bill stands in an approval chain; null when no workflow was involved and the ordinary approve action applied.</summary>
    public ApprovalStatus? ApprovalStatus { get; set; }

    /// <summary>The level an in-approval bill waits on, as the workflow labels it.</summary>
    [MaxLength(50, ErrorMessage = "The step label cannot exceed 50 characters.")]
    public string? ApprovalStep { get; set; }
}

/// <summary>The posted bills the filters match added up, in base currency. A draft is not in the books and a voided bill is withdrawn, so both are left out.</summary>
public sealed class BillsTrackTotalsDto
{
    public decimal Total { get; set; }

    public decimal Paid { get; set; }

    public decimal Due { get; set; }
}

public sealed class BillsTrackReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of bills, newest first.</summary>
    public List<BillsTrackRowDto> Bills { get; set; } = [];

    /// <summary>How many bills the filters match, all pages.</summary>
    public int TotalBills { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many bills have each status, for the tabs: every status is present, none included. Void is counted but is not part of All.</summary>
    public Dictionary<BillTrackStatus, int> StatusCounts { get; set; } = [];

    /// <summary>Added up over every posted bill the filters match that still stands, not only this page.</summary>
    public BillsTrackTotalsDto Totals { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Receive Order Track Report (TK-242): each goods receipt's progress with its bills: received, billed
// in full or in part, or still to bill. Benchmarked on AutoCount's GRN Tracking and Odoo's receipts with
// billing status. A receipt's value is what its accepted goods are worth, at the cost the receipt itself
// stocked them at (the line's taxable value shared out over what was delivered), in the branch's base
// currency; billed is the share of it that posted bills name, at that same cost, so received less billed is
// what is still to bill, on every receipt. A receipt in another currency also carries its own value.

/// <summary>
/// What the report is asked for: the receipt-date period, both dates required (D-67), optionally one vendor
/// and one status, only receipts with something still to bill, a page of receipts at a time. The branch is
/// always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class ReceiveOrderTrackReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this vendor; every vendor when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only receipts with this status. Blank lists every status.</summary>
    public GoodsReceiptBillingStatus? Status { get; set; }

    /// <summary>Only receipts with something still to bill: not billed, or partly billed.</summary>
    public bool NotYetBilledOnly { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>A receipt with its value and what has been billed of it.</summary>
public sealed class ReceiveOrderTrackReceiptDto
{
    public long GoodsReceiptId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The purchase order the receipt was raised against; blank for goods that came with none.</summary>
    public long? PurchaseOrderId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string? PurchaseOrderNo { get; set; }

    /// <summary>The vendor's own delivery note.</summary>
    [MaxLength(50, ErrorMessage = "Delivery note number cannot exceed 50 characters.")]
    public string? VendorDeliveryNoteNo { get; set; }

    public GoodsReceiptBillingStatus Status { get; set; }

    /// <summary>What the accepted goods are worth, in the receipt's own currency.</summary>
    public decimal ValueInCurrency { get; set; }

    /// <summary>What the accepted goods are worth, in the branch's base currency.</summary>
    public decimal Value { get; set; }

    /// <summary>The share of the value that posted bills name, in base currency.</summary>
    public decimal BilledValue { get; set; }

    /// <summary>Value less billed value, in base currency.</summary>
    public decimal ToBillValue { get; set; }
}

/// <summary>The receipts the filters match added up, in base currency.</summary>
public sealed class ReceiveOrderTrackTotalsDto
{
    public decimal Value { get; set; }

    public decimal BilledValue { get; set; }

    public decimal ToBillValue { get; set; }
}

public sealed class ReceiveOrderTrackReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of receipts, newest first.</summary>
    public List<ReceiveOrderTrackReceiptDto> Receipts { get; set; } = [];

    /// <summary>How many receipts the filters match, all pages.</summary>
    public int TotalReceipts { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many receipts have each status, for the tabs: every status is present, none included.</summary>
    public Dictionary<GoodsReceiptBillingStatus, int> StatusCounts { get; set; } = [];

    /// <summary>Added up over every receipt the filters match, not only this page.</summary>
    public ReceiveOrderTrackTotalsDto Totals { get; set; } = new();
}

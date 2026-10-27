using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Purchase Order Track Report (TK-240): each purchase order's progress: ordered, received and
// billed, line by line, and what is still to receive or bill. Benchmarked on AutoCount's Purchase
// Order Outstanding and Odoo's purchase orders with receipt and bill status. Quantities are in each
// line's own unit; values are in the branch's base currency, before tax, at the order's own price per
// unit, so ordered less received is what is still to receive and ordered less billed what is still to
// bill, on every line.

/// <summary>
/// What the report is asked for: the order-date period, both dates required (D-67), optionally one
/// vendor and one progress, only orders with something still outstanding, a page of orders at a time.
/// The branch is always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class PurchaseOrderTrackReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this vendor; every vendor when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only orders at this progress. Blank lists every progress but cancelled.</summary>
    public PurchaseOrderProgress? Progress { get; set; }

    /// <summary>Only orders with something still to receive or bill: open, partly received or received and not billed.</summary>
    public bool OutstandingOnly { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One line of an order: what was ordered, received and billed of it, and what is outstanding.</summary>
public sealed class PurchaseOrderTrackLineDto
{
    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    /// <summary>Blank on a line that is not for an item.</summary>
    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string? ItemCode { get; set; }

    /// <summary>The item's name, else the line's own description.</summary>
    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "Unit code cannot exceed 20 characters.")]
    public string? UnitCode { get; set; }

    public decimal Ordered { get; set; }

    /// <summary>What has arrived and been accepted across the goods receipts against the line.</summary>
    public decimal Received { get; set; }

    /// <summary>What posted bills have billed against the line, directly or through a goods receipt.</summary>
    public decimal Billed { get; set; }

    /// <summary>Ordered less received.</summary>
    public decimal ToReceive { get; set; }

    /// <summary>Ordered less billed.</summary>
    public decimal ToBill { get; set; }

    /// <summary>The line's net value before tax, in base currency.</summary>
    public decimal OrderedValue { get; set; }

    /// <summary>The received share of the line's value, at the order's price.</summary>
    public decimal ReceivedValue { get; set; }

    public decimal BilledValue { get; set; }

    /// <summary>Ordered value less received value.</summary>
    public decimal ToReceiveValue { get; set; }

    /// <summary>Ordered value less billed value.</summary>
    public decimal ToBillValue { get; set; }
}

/// <summary>An order with its lines and what has been billed against it.</summary>
public sealed class PurchaseOrderTrackOrderDto
{
    public long PurchaseOrderId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    /// <summary>The date the vendor is expected to deliver, when there is one.</summary>
    public DateOnly? ExpectedDate { get; set; }

    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    public PurchaseOrderProgress Progress { get; set; }

    public List<PurchaseOrderTrackLineDto> Lines { get; set; } = [];

    public decimal OrderedValue { get; set; }

    public decimal ReceivedValue { get; set; }

    public decimal BilledValue { get; set; }

    public decimal ToReceiveValue { get; set; }

    public decimal ToBillValue { get; set; }

    /// <summary>How many posted bills have been raised against the order's lines.</summary>
    public int BillsRaised { get; set; }

    /// <summary>What those bills' lines came to before tax, in base currency, at the prices they were actually billed at.</summary>
    public decimal BillTotal { get; set; }
}

/// <summary>The orders the filters match added up, in base currency, before tax.</summary>
public sealed class PurchaseOrderTrackTotalsDto
{
    public decimal OrderedValue { get; set; }

    public decimal ReceivedValue { get; set; }

    public decimal BilledValue { get; set; }

    public decimal ToReceiveValue { get; set; }

    public decimal ToBillValue { get; set; }
}

public sealed class PurchaseOrderTrackReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of orders, newest first.</summary>
    public List<PurchaseOrderTrackOrderDto> Orders { get; set; } = [];

    /// <summary>How many orders the filters match, all pages.</summary>
    public int TotalOrders { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many orders are at each progress, for the tabs: every progress is present, none included. Cancelled is counted but is not part of All.</summary>
    public Dictionary<PurchaseOrderProgress, int> ProgressCounts { get; set; } = [];

    /// <summary>Added up over every order the filters match, not only this page.</summary>
    public PurchaseOrderTrackTotalsDto Totals { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Delivery Order Track Report (TK-232): each delivery challan dated in a period, how far it has been
// billed, line by line, and what it was delivered against. Benchmarked on AutoCount's Delivery Order
// Tracking and Odoo's Deliveries with billing status. Quantities are in each line's own unit; values are
// before tax in the branch's base currency, at the challan's own price per unit.

/// <summary>
/// What the report is asked for: the challan-date period, both dates required (D-67), optionally one
/// customer, one status and one challan type, a page of challans at a time. The branch is always the
/// token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class DeliveryOrderTrackReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only challans at this status. Blank lists every status but void, which is listed only when asked for.</summary>
    public ChallanProgress? Progress { get; set; }

    /// <summary>Only challans of this type: why the goods left. Every type when blank.</summary>
    public ChallanType? ChallanType { get; set; }

    /// <summary>Only challans with something still to bill: not invoiced, or partly invoiced.</summary>
    public bool NotInvoicedOnly { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One line of a challan: what was delivered and how much of it has been billed.</summary>
public sealed class DeliveryOrderTrackLineDto
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

    /// <summary>What was delivered.</summary>
    public decimal Delivered { get; set; }

    /// <summary>What invoices raised from the challan have billed.</summary>
    public decimal Invoiced { get; set; }

    /// <summary>Delivered less invoiced.</summary>
    public decimal ToBill { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }

    /// <summary>Delivered value less invoiced value.</summary>
    public decimal ToBillValue { get; set; }
}

/// <summary>An invoice raised from a challan.</summary>
public sealed class DeliveryOrderTrackInvoiceDto
{
    /// <summary>INV, or POS for a sale rung up at the till.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }
}

/// <summary>A challan with its lines, the order it was delivered against and the invoices raised from it.</summary>
public sealed class DeliveryOrderTrackChallanDto
{
    public long DeliveryChallanId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    /// <summary>The day the goods went out.</summary>
    public DateOnly DispatchDate { get; set; }

    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Why the goods left: a sale, job work, approval, a branch transfer or a sample.</summary>
    public ChallanType ChallanType { get; set; }

    public ChallanProgress Progress { get; set; }

    /// <summary>The sales order it was delivered against; null when there is none.</summary>
    public long? SalesOrderId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string? SalesOrderNo { get; set; }

    public List<DeliveryOrderTrackLineDto> Lines { get; set; } = [];

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }

    public decimal ToBillValue { get; set; }

    /// <summary>The posted invoices raised from the challan, earliest first.</summary>
    public List<DeliveryOrderTrackInvoiceDto> Invoices { get; set; } = [];
}

/// <summary>The values of every challan the filters match, added up.</summary>
public sealed class DeliveryOrderTrackTotalsDto
{
    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }

    public decimal ToBillValue { get; set; }
}

public sealed class DeliveryOrderTrackReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of challans, newest first.</summary>
    public List<DeliveryOrderTrackChallanDto> Challans { get; set; } = [];

    /// <summary>How many challans the filters match, all pages.</summary>
    public int TotalChallans { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many challans are at each status, for the tabs: every status is present, none included. Void is counted but is not part of All.</summary>
    public Dictionary<ChallanProgress, int> ProgressCounts { get; set; } = [];

    /// <summary>Added up over every challan the filters match, not only this page.</summary>
    public DeliveryOrderTrackTotalsDto Totals { get; set; } = new();
}

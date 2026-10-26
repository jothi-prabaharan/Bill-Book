using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Sales Order Track Report (TK-230): each sales order's progress: ordered, delivered and
// invoiced, line by line, and what is still to deliver or bill. Benchmarked on AutoCount's Sales
// Order Outstanding and Odoo's sales orders with delivery and invoice status. Quantities are in each
// line's own unit; values are in the branch's base currency, before tax, at the order's own price per
// unit, so ordered less delivered is what is still to deliver and ordered less invoiced what is still
// to bill, on every line.

/// <summary>
/// What the report is asked for: the order-date period, both dates required (D-67), optionally one
/// customer and one progress, only orders with something still outstanding, a page of orders at a
/// time. The branch is always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class SalesOrderTrackReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only orders at this progress. Blank lists every progress but cancelled.</summary>
    public SalesOrderProgress? Progress { get; set; }

    /// <summary>Only orders with something still to deliver or bill: open, partly delivered or delivered but not invoiced.</summary>
    public bool OutstandingOnly { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One line of an order: what was ordered, delivered and invoiced of it, and what is outstanding.</summary>
public sealed class SalesOrderTrackLineDto
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

    public decimal Delivered { get; set; }

    public decimal Invoiced { get; set; }

    /// <summary>Ordered less delivered.</summary>
    public decimal ToDeliver { get; set; }

    /// <summary>Ordered less invoiced.</summary>
    public decimal ToBill { get; set; }

    /// <summary>The line's net value before tax, in base currency.</summary>
    public decimal OrderedValue { get; set; }

    /// <summary>The delivered share of the line's value, at the order's price.</summary>
    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }

    /// <summary>Ordered value less delivered value.</summary>
    public decimal ToDeliverValue { get; set; }

    /// <summary>Ordered value less invoiced value.</summary>
    public decimal ToBillValue { get; set; }
}

/// <summary>An order with its lines and what it has had raised against it.</summary>
public sealed class SalesOrderTrackOrderDto
{
    public long SalesOrderId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    /// <summary>The date the customer was promised it, when there is one.</summary>
    public DateOnly? DeliveryDate { get; set; }

    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    public SalesOrderProgress Progress { get; set; }

    public List<SalesOrderTrackLineDto> Lines { get; set; } = [];

    public decimal OrderedValue { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }

    public decimal ToDeliverValue { get; set; }

    public decimal ToBillValue { get; set; }

    /// <summary>How many posted invoices have been raised from the order.</summary>
    public int InvoicesRaised { get; set; }

    /// <summary>What those invoices came to before tax, in base currency, at the prices they were actually raised at.</summary>
    public decimal Billed { get; set; }
}

/// <summary>The orders the filters match added up, in base currency, before tax.</summary>
public sealed class SalesOrderTrackTotalsDto
{
    public decimal OrderedValue { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }

    public decimal ToDeliverValue { get; set; }

    public decimal ToBillValue { get; set; }
}

public sealed class SalesOrderTrackReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of orders, newest first.</summary>
    public List<SalesOrderTrackOrderDto> Orders { get; set; } = [];

    /// <summary>How many orders the filters match, all pages.</summary>
    public int TotalOrders { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many orders are at each progress, for the tabs: every progress is present, none included. Cancelled is counted but is not part of All.</summary>
    public Dictionary<SalesOrderProgress, int> ProgressCounts { get; set; } = [];

    /// <summary>Added up over every order the filters match, not only this page.</summary>
    public SalesOrderTrackTotalsDto Totals { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

// The Purchase Receive Order Details (TK-241): every line of every goods receipt dated in a period,
// what arrived, from whom, against which order, into which warehouse and at what cost. Benchmarked
// on AutoCount's Goods Received Note Listing and Odoo's Receipts detail. A line is in its receipt's
// own currency, valued at the cost Purchase stocked it at (the line's taxable value shared out over
// what was delivered); the totals of every receipt the filters match are in the branch's base
// currency.

/// <summary>
/// What the report is asked for: the receipt-date period, both dates required (D-67); optionally one
/// vendor, one item, one warehouse and one purchase order; a page of receipts at a time. An item or a
/// warehouse narrows the lines shown as well as the receipts: only a receipt with such a line is
/// listed, and only those lines. The branch is always the token's: a request naming another branch is
/// refused with 403.
/// </summary>
public sealed class PurchaseReceiveOrderDetailsReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this vendor; every vendor when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only receipts with a line for this item, and only those lines.</summary>
    public long? ItemId { get; set; }

    /// <summary>Only receipts with a line taken into this warehouse, and only those lines.</summary>
    public long? WarehouseId { get; set; }

    /// <summary>Only receipts against this purchase order.</summary>
    public long? PurchaseOrderId { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One line of a receipt, in the receipt's own currency.</summary>
public sealed class PurchaseReceiveOrderDetailsLineDto
{
    public long GoodsReceiptDetailId { get; set; }

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

    /// <summary>Where the line was taken into stock; blank when it names none.</summary>
    public long? WarehouseId { get; set; }

    [MaxLength(150, ErrorMessage = "Warehouse name cannot exceed 150 characters.")]
    public string? WarehouseName { get; set; }

    /// <summary>What the vendor delivered.</summary>
    public decimal Delivered { get; set; }

    /// <summary>What was refused at the door: never taken into stock and not owed for.</summary>
    public decimal Rejected { get; set; }

    /// <summary>What was accepted and went into stock: the quantity of the stock-in movement the receipt made.</summary>
    public decimal Accepted { get; set; }

    /// <summary>What posted bills have billed against the line.</summary>
    public decimal Billed { get; set; }

    /// <summary>What one delivered unit cost, net of discount and before tax.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>The accepted quantity at that cost: what the receipt debits to Inventory.</summary>
    public decimal Value { get; set; }

    /// <summary>The value in the branch's base currency, at the receipt's rate.</summary>
    public decimal ValueBase { get; set; }

    [MaxLength(500, ErrorMessage = "Rejection reason cannot exceed 500 characters.")]
    public string? RejectionReason { get; set; }

    [MaxLength(50, ErrorMessage = "Batch number cannot exceed 50 characters.")]
    public string? BatchNumber { get; set; }

    public DateOnly? BatchExpiryDate { get; set; }

    /// <summary>The serial numbers the receipt created for the line: the first of them, when there are very many.</summary>
    public List<string> Serials { get; set; } = [];

    /// <summary>How many serial numbers the receipt created for the line.</summary>
    public int SerialCount { get; set; }
}

/// <summary>A receipt with its lines.</summary>
public sealed class PurchaseReceiveOrderDetailsReceiptDto
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

    public DateOnly? VendorDeliveryNoteDate { get; set; }

    public List<PurchaseReceiveOrderDetailsLineDto> Lines { get; set; } = [];

    /// <summary>The lines shown added up, in the receipt's own currency.</summary>
    public decimal Value { get; set; }

    /// <summary>The same in the branch's base currency.</summary>
    public decimal ValueBase { get; set; }
}

/// <summary>The receipts the filters match added up, in base currency.</summary>
public sealed class PurchaseReceiveOrderDetailsTotalsDto
{
    /// <summary>How many lines the matching receipts have, the narrowed ones when an item or warehouse is chosen.</summary>
    public int Lines { get; set; }

    public decimal Value { get; set; }
}

public sealed class PurchaseReceiveOrderDetailsReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>An item or a warehouse narrows the lines, so a receipt's value is of the lines shown and not of the whole receipt.</summary>
    public bool LinesFiltered { get; set; }

    /// <summary>This page of receipts, newest first.</summary>
    public List<PurchaseReceiveOrderDetailsReceiptDto> Receipts { get; set; } = [];

    /// <summary>How many receipts the filters match, all pages.</summary>
    public int TotalReceipts { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>Added up over every receipt the filters match, not only this page.</summary>
    public PurchaseReceiveOrderDetailsTotalsDto Totals { get; set; } = new();
}

/// <summary>A purchase order the filter can pick: one a posted goods receipt was raised against.</summary>
public sealed class PurchaseReceiveOrderDetailsOrderChoiceDto
{
    public long PurchaseOrderId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;
}

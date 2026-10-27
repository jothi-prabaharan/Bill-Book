using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Payable Invoice Detail (TK-236): the Payable Invoice Summary line by line, which items,
// quantities, prices, discounts and taxes make up each vendor bill. Benchmarked on Xero's Payable
// Invoice Detail and AutoCount's Purchase Invoice Detail Listing. A line is in its bill's own
// currency; the totals of every bill the filters match are in the branch's base currency. The
// bills, their statuses and the status counts are the summary's.

/// <summary>
/// What the report is asked for: the period the bills are dated in, both dates required (D-67);
/// optionally one vendor, one item, one item category and one status; a page of bills at a time.
/// An item or category narrows the lines shown as well as the bills: only a bill with such a line
/// is listed, and only those lines. The branch is always the token's: a request naming another
/// branch is refused with 403.
/// </summary>
public sealed class PayableInvoiceDetailReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this vendor; every vendor when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only bills with a line for this item, and only those lines.</summary>
    public long? ItemId { get; set; }

    /// <summary>Only bills with a line for an item in this category, and only those lines.</summary>
    public long? ItemCategoryId { get; set; }

    /// <summary>Only this status. Blank lists every status but void, which is listed only when asked for.</summary>
    public PayableInvoiceStatus? Status { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One line of a bill, in the bill's own currency.</summary>
public sealed class PayableInvoiceDetailLineDto
{
    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    /// <summary>Blank on a line that is not for an item, such as a service or an expense typed in.</summary>
    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string? ItemCode { get; set; }

    /// <summary>The item's name, else the line's own description.</summary>
    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>The line's own description, when it has one and it is not the name shown.</summary>
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>The line is a fixed asset bought on the bill, which also puts it in the asset register.</summary>
    public bool IsCapital { get; set; }

    /// <summary>The fixed-asset category of a capital line.</summary>
    [MaxLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
    public string? AssetCategoryName { get; set; }

    public decimal Quantity { get; set; }

    [MaxLength(20, ErrorMessage = "Unit code cannot exceed 20 characters.")]
    public string? UnitCode { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>The discount as a percentage, when it was entered as one.</summary>
    public decimal? DiscountPercent { get; set; }

    public decimal Taxable { get; set; }

    /// <summary>The rates of the line's tax rows added up, such as 18 for 9 and 9 or for 18; nought when it has none.</summary>
    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }
}

/// <summary>A bill with its lines. Its figures are in its own currency, its base total beside it.</summary>
public sealed class PayableInvoiceDetailBillDto
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

    public DateOnly? DueDate { get; set; }

    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    public PayableInvoiceStatus Status { get; set; }

    /// <summary>The tax on the bill is the branch's own to pay, not the vendor's to be paid.</summary>
    public bool ReverseCharge { get; set; }

    /// <summary>The bill's total in its own currency, round-off included, as on the summary.</summary>
    public decimal TotalInCurrency { get; set; }

    /// <summary>The same total in base currency.</summary>
    public decimal Total { get; set; }

    /// <summary>What the rupee rounding added; null when an item or category narrows the lines, since then they are not the whole bill.</summary>
    public decimal? RoundOff { get; set; }

    public List<PayableInvoiceDetailLineDto> Lines { get; set; } = [];

    /// <summary>The lines shown added up, in the bill's own currency.</summary>
    public decimal LinesTaxable { get; set; }

    public decimal LinesTax { get; set; }

    public decimal LinesTotal { get; set; }
}

/// <summary>The lines shown, in every bill the filters match, in base currency.</summary>
public sealed class PayableInvoiceDetailTotalsDto
{
    public decimal Taxable { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }
}

public sealed class PayableInvoiceDetailReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>True when an item or category narrows the lines, so a bill's lines are not the whole of it.</summary>
    public bool LinesFiltered { get; set; }

    /// <summary>This page of bills, newest first.</summary>
    public List<PayableInvoiceDetailBillDto> Bills { get; set; } = [];

    /// <summary>How many bills the filters match, all pages.</summary>
    public int TotalBills { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many bills have each status, for the tabs: every status is present, none included. Void is counted but is not part of All.</summary>
    public Dictionary<PayableInvoiceStatus, int> StatusCounts { get; set; } = [];

    /// <summary>Added up over every bill the filters match, not only this page.</summary>
    public PayableInvoiceDetailTotalsDto Totals { get; set; } = new();
}

/// <summary>An item the filter can pick.</summary>
public sealed class PayableInvoiceItemChoiceDto
{
    public long ItemId { get; set; }

    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string ItemCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;
}

/// <summary>An item category the filter can pick.</summary>
public sealed class PayableInvoiceCategoryChoiceDto
{
    public long ItemCategoryId { get; set; }

    [MaxLength(50, ErrorMessage = "Category code cannot exceed 50 characters.")]
    public string CategoryCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
    public string CategoryName { get; set; } = string.Empty;
}

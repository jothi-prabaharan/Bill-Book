using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Receivable Invoice Detail (TK-225): the Receivable Invoice Summary line by line, which
// items, quantities, prices, discounts and taxes make up each invoice. Benchmarked on Xero's
// Receivable Invoice Detail and AutoCount's Sales Invoice Detail Listing. A line is in its
// invoice's own currency; the totals of every invoice the filters match are in the branch's
// base currency. The invoices, their statuses and the status counts are the summary's.

/// <summary>
/// What the report is asked for: the period the invoices are dated in, both dates required
/// (D-67); optionally one customer, one item, one item category and one status; a page of
/// invoices at a time. An item or category narrows the lines shown as well as the invoices:
/// only an invoice with such a line is listed, and only those lines. The branch is always the
/// token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class ReceivableInvoiceDetailReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only invoices with a line for this item, and only those lines.</summary>
    public long? ItemId { get; set; }

    /// <summary>Only invoices with a line for an item in this category, and only those lines.</summary>
    public long? ItemCategoryId { get; set; }

    /// <summary>Only this status. Blank lists every status but void, which is listed only when asked for.</summary>
    public ReceivableInvoiceStatus? Status { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One line of an invoice, in the invoice's own currency.</summary>
public sealed class ReceivableInvoiceDetailLineDto
{
    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    /// <summary>Blank on a line that is not for an item, such as a service typed in or an expense.</summary>
    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string? ItemCode { get; set; }

    /// <summary>The item's name, else the line's own description.</summary>
    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>The line's own description, when it has one and it is not the name shown.</summary>
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

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

/// <summary>An invoice with its lines. Its figures are in its own currency, its base total beside it.</summary>
public sealed class ReceivableInvoiceDetailInvoiceDto
{
    /// <summary>INV, or POS for a sale rung up at the till.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long ContactId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string ContactCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = string.Empty;

    public ReceivableInvoiceStatus Status { get; set; }

    /// <summary>The invoice's total in its own currency, round-off included, as on the summary.</summary>
    public decimal TotalInCurrency { get; set; }

    /// <summary>The same total in base currency.</summary>
    public decimal Total { get; set; }

    /// <summary>What the rupee rounding added; null when an item or category narrows the lines, since then they are not the whole invoice.</summary>
    public decimal? RoundOff { get; set; }

    public List<ReceivableInvoiceDetailLineDto> Lines { get; set; } = [];

    /// <summary>The lines shown added up, in the invoice's own currency.</summary>
    public decimal LinesTaxable { get; set; }

    public decimal LinesTax { get; set; }

    public decimal LinesTotal { get; set; }
}

/// <summary>The lines shown, in every invoice the filters match, in base currency.</summary>
public sealed class ReceivableInvoiceDetailTotalsDto
{
    public decimal Taxable { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }
}

public sealed class ReceivableInvoiceDetailReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>True when an item or category narrows the lines, so an invoice's lines are not the whole of it.</summary>
    public bool LinesFiltered { get; set; }

    /// <summary>This page of invoices, newest first.</summary>
    public List<ReceivableInvoiceDetailInvoiceDto> Invoices { get; set; } = [];

    /// <summary>How many invoices the filters match, all pages.</summary>
    public int TotalInvoices { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>How many invoices have each status, for the tabs: every status is present, none included. Void is counted but is not part of All.</summary>
    public Dictionary<ReceivableInvoiceStatus, int> StatusCounts { get; set; } = [];

    /// <summary>Added up over every invoice the filters match, not only this page.</summary>
    public ReceivableInvoiceDetailTotalsDto Totals { get; set; } = new();
}

/// <summary>An item the filter can pick.</summary>
public sealed class ReceivableInvoiceItemChoiceDto
{
    public long ItemId { get; set; }

    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string ItemCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;
}

/// <summary>An item category the filter can pick.</summary>
public sealed class ReceivableInvoiceCategoryChoiceDto
{
    public long ItemCategoryId { get; set; }

    [MaxLength(50, ErrorMessage = "Category code cannot exceed 50 characters.")]
    public string CategoryCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
    public string CategoryName { get; set; } = string.Empty;
}

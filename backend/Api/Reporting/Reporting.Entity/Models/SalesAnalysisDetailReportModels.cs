using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Sales Analysis Detail Report (TK-229): the Sales Analysis before it is added up, one row for
// each invoice or credit note line, grouped by item, item category or customer with a subtotal for
// each group. Benchmarked on AutoCount's Sales Analysis Detail and Odoo's sales analysis list view.
// Everything is in the branch's base currency, and the lines are the Sales Analysis's own, so each
// group's subtotal is that report's row for the group.

/// <summary>
/// What the report is asked for: the invoice-date period, both dates required (D-67), what to group by,
/// optionally one item, one item category, one customer and one warehouse, and whether credit notes are
/// taken off (they are, unless turned off). The branch is always the token's: a request naming another
/// branch is refused with 403.
/// </summary>
public sealed class SalesAnalysisDetailReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>What to group the lines by; items when blank.</summary>
    public SalesAnalysisGroupBy GroupBy { get; set; } = SalesAnalysisGroupBy.Item;

    /// <summary>Only lines for this item; every item when blank.</summary>
    public long? ItemId { get; set; }

    /// <summary>Only lines for items in this category; every category when blank.</summary>
    public long? ItemCategoryId { get; set; }

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only lines sold out of this warehouse; every warehouse when blank.</summary>
    public long? WarehouseId { get; set; }

    /// <summary>Whether posted credit notes' lines are listed, as negatives. On unless asked otherwise.</summary>
    public bool IncludeCreditNotes { get; set; } = true;

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 50;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One invoice or credit note line, in base currency; a credit note's amounts are negative.</summary>
public sealed class SalesAnalysisDetailLineDto
{
    /// <summary>Unique across the report: an invoice line's id, or a credit note line's id negated.</summary>
    public long LineKey { get; set; }

    /// <summary>INV or POS for an invoice, CRN for a credit note.</summary>
    [MaxLength(3, ErrorMessage = "Transaction type code cannot exceed 3 characters.")]
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public int LineNumber { get; set; }

    public long ContactId { get; set; }

    [MaxLength(150, ErrorMessage = "Contact name cannot exceed 150 characters.")]
    public string ContactName { get; set; } = string.Empty;

    /// <summary>Nought for a line with no item.</summary>
    public long ItemId { get; set; }

    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string ItemCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Units, in the item's base unit; negative on a sales return and nought on a credit note that gives no goods back.</summary>
    public decimal Quantity { get; set; }

    /// <summary>The price of one unit, in base currency.</summary>
    public decimal UnitPrice { get; set; }

    public decimal Gross { get; set; }

    public decimal Discount { get; set; }

    /// <summary>Net sales: before tax, after discount.</summary>
    public decimal Net { get; set; }

    /// <summary>What the line cost, when its stock movement has been costed; a line pending its cost shows nothing here.</summary>
    public decimal Cost { get; set; }

    /// <summary>Whether the line's cost has not been settled yet, so it has no margin.</summary>
    public bool CostPending { get; set; }

    /// <summary>Net sales less cost; null while the cost is pending.</summary>
    public decimal? Margin { get; set; }
}

/// <summary>A group with the lines of it on this page, and its subtotal over every line it has.</summary>
public sealed class SalesAnalysisDetailGroupDto
{
    /// <summary>The item, category or customer id; nought for lines with none.</summary>
    public long Key { get; set; }

    [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters.")]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>The group's lines on this page; a group can carry on over the next.</summary>
    public List<SalesAnalysisDetailLineDto> Lines { get; set; } = [];

    /// <summary>How many lines the group has in all, not only on this page.</summary>
    public int LineCount { get; set; }

    /// <summary>What the group's lines add up to, all of them: the Sales Analysis's row for the group.</summary>
    public SalesAnalysisGroupDto Subtotal { get; set; } = new();
}

public sealed class SalesAnalysisDetailReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    public SalesAnalysisGroupBy GroupBy { get; set; }

    public bool IncludesCreditNotes { get; set; }

    /// <summary>The groups whose lines fall on this page, in order, each with its lines of the page.</summary>
    public List<SalesAnalysisDetailGroupDto> Groups { get; set; } = [];

    /// <summary>How many lines the filters match, all pages.</summary>
    public int TotalLines { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>Everything the filters match added up, not only this page.</summary>
    public SalesAnalysisTotalsDto Totals { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Sales Analysis (TK-228): what sold in a period, added up by item, item category or customer:
// quantity, gross value, discount, net sales, cost of sales and margin. Benchmarked on Xero's Sales
// by Item and Sales by Customer, AutoCount's Sales Analysis and Odoo's Sales Analysis. Everything is
// in the branch's base currency; net sales are before tax. Posted invoices and POS sales, less posted
// credit notes. A line whose cost has not been settled yet is counted and its cost is said to be
// pending, never taken as nothing.

/// <summary>
/// What the report is asked for: the invoice-date period, both dates required (D-67), what to add
/// sales up by, optionally one customer, one item category and one warehouse, whether credit notes
/// reduce the figures (they do, unless turned off), and optionally a second period to compare net
/// sales with. The branch is always the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class SalesAnalysisReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>What to add sales up by; items when blank.</summary>
    public SalesAnalysisGroupBy GroupBy { get; set; } = SalesAnalysisGroupBy.Item;

    /// <summary>Only this customer; every customer when blank.</summary>
    public long? ContactId { get; set; }

    /// <summary>Only items in this category; every category when blank.</summary>
    public long? ItemCategoryId { get; set; }

    /// <summary>Only lines sold out of this warehouse; every warehouse when blank.</summary>
    public long? WarehouseId { get; set; }

    /// <summary>Whether posted credit notes reduce the figures. On unless asked otherwise: a return is not a sale.</summary>
    public bool IncludeCreditNotes { get; set; } = true;

    /// <summary>The start of a second period to compare net sales with; with <see cref="CompareToDate"/>, both or neither.</summary>
    public DateOnly? CompareFromDate { get; set; }

    /// <summary>The end of the second period.</summary>
    public DateOnly? CompareToDate { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 50;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One item, category or customer with what it sold, in base currency.</summary>
public sealed class SalesAnalysisGroupDto
{
    /// <summary>The item, category or customer id; nought for the row of lines with none.</summary>
    public long Key { get; set; }

    [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters.")]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Units sold less units returned, in each item's base unit.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Quantity times price, as keyed: a price that includes tax shows it here.</summary>
    public decimal Gross { get; set; }

    public decimal Discount { get; set; }

    /// <summary>What was sold for before tax, after discount.</summary>
    public decimal Net { get; set; }

    /// <summary>What the costed lines cost; a line not costed yet is not in it.</summary>
    public decimal Cost { get; set; }

    /// <summary>How many lines have no settled cost yet, so the cost and margin are not complete.</summary>
    public int CostPendingLines { get; set; }

    /// <summary>Net sales less cost; null while any line's cost is pending.</summary>
    public decimal? Margin { get; set; }

    /// <summary>The margin as a share of net sales; null while any cost is pending or when there are no net sales.</summary>
    public decimal? MarginPercent { get; set; }

    /// <summary>Net sales in the comparison period; null when none was asked for.</summary>
    public decimal? CompareNet { get; set; }

    /// <summary>How much net sales grew or fell against the comparison period, as a percentage of it; null when none was asked for or it had no sales.</summary>
    public decimal? ChangePercent { get; set; }
}

/// <summary>Everything the filters match added up, not only the groups on this page.</summary>
public sealed class SalesAnalysisTotalsDto
{
    public decimal Quantity { get; set; }

    public decimal Gross { get; set; }

    public decimal Discount { get; set; }

    public decimal Net { get; set; }

    public decimal Cost { get; set; }

    public int CostPendingLines { get; set; }

    public decimal? Margin { get; set; }

    public decimal? MarginPercent { get; set; }

    public decimal? CompareNet { get; set; }

    public decimal? ChangePercent { get; set; }
}

public sealed class SalesAnalysisReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public DateOnly? CompareFromDate { get; set; }

    public DateOnly? CompareToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    public SalesAnalysisGroupBy GroupBy { get; set; }

    public bool IncludesCreditNotes { get; set; }

    /// <summary>This page of groups, largest net sales first.</summary>
    public List<SalesAnalysisGroupDto> Groups { get; set; } = [];

    /// <summary>How many groups the filters match, all pages.</summary>
    public int TotalGroups { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public SalesAnalysisTotalsDto Totals { get; set; } = new();
}

/// <summary>A warehouse the filter can pick.</summary>
public sealed class SalesAnalysisWarehouseChoiceDto
{
    public long WarehouseId { get; set; }

    [MaxLength(50, ErrorMessage = "Warehouse code cannot exceed 50 characters.")]
    public string WarehouseCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Warehouse name cannot exceed 150 characters.")]
    public string WarehouseName { get; set; } = string.Empty;
}

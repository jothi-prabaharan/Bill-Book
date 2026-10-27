using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

// The Inventory Item Summary (TK-243): for each stocked item over a period, its opening quantity and
// value, what was purchased, sold and adjusted, its closing quantity and value, and the profit on what
// was sold. Benchmarked on Xero's Inventory Item Summary, AutoCount's Stock Card summary and Odoo's Stock
// valuation. Quantities are in the item's own inventory unit; values are in the branch's base currency.
// Stock is one pool per branch: warehouses are a location only, so nothing here is split by warehouse.

/// <summary>
/// What the report is asked for: the period, both dates required (D-67), optionally one item category and
/// one item, and whether to list items that did not move; a page of items at a time. The branch is always
/// the token's: a request naming another branch is refused with 403.
/// </summary>
public sealed class InventoryItemSummaryReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only items in this category.</summary>
    public long? ItemCategoryId { get; set; }

    /// <summary>Only this item.</summary>
    public long? ItemId { get; set; }

    /// <summary>List every stocked item, including those with no stock at the From date and no movement in the period.</summary>
    public bool ShowItemsWithNoMovement { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One item's stock over the period.</summary>
public sealed class InventoryItemSummaryItemDto
{
    public long ItemId { get; set; }

    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string ItemCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>The item's category; blank for none.</summary>
    [MaxLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
    public string? CategoryName { get; set; }

    public CostingType CostingType { get; set; }

    /// <summary>The unit every quantity of the item is in.</summary>
    [MaxLength(20, ErrorMessage = "Unit code cannot exceed 20 characters.")]
    public string? UnitCode { get; set; }

    /// <summary>The stock at the start of the From date: every movement before it.</summary>
    public decimal OpeningQuantity { get; set; }

    public decimal OpeningValue { get; set; }

    /// <summary>Goods received less goods sent back to the vendors, in the period.</summary>
    public decimal PurchasedQuantity { get; set; }

    public decimal PurchasedValue { get; set; }

    /// <summary>Goods issued to customers less goods they sent back, in the period.</summary>
    public decimal SoldQuantity { get; set; }

    /// <summary>What the quantity sold was sold for: net sales before tax of the invoices less credit notes, in base currency.</summary>
    public decimal SalesValue { get; set; }

    /// <summary>What the quantity sold cost, at the cost the stock moved out at.</summary>
    public decimal Cogs { get; set; }

    /// <summary>Sales less cost of sales; blank while a movement of what was sold has not been costed yet.</summary>
    public decimal? Profit { get; set; }

    /// <summary>Every other movement in the period, signed: counts, opening entries, transfers.</summary>
    public decimal AdjustedQuantity { get; set; }

    public decimal AdjustedValue { get; set; }

    /// <summary>Opening plus purchased less sold plus adjusted.</summary>
    public decimal ClosingQuantity { get; set; }

    public decimal ClosingValue { get; set; }

    /// <summary>A movement up to the To date has not been costed yet, so the values are short by its cost.</summary>
    public bool CostPending { get; set; }
}

/// <summary>The items the filters match added up: values only, since quantities in different units do not add.</summary>
public sealed class InventoryItemSummaryTotalsDto
{
    public decimal OpeningValue { get; set; }

    public decimal PurchasedValue { get; set; }

    public decimal SalesValue { get; set; }

    public decimal Cogs { get; set; }

    /// <summary>Blank while a movement of something sold has not been costed yet.</summary>
    public decimal? Profit { get; set; }

    public decimal AdjustedValue { get; set; }

    public decimal ClosingValue { get; set; }

    /// <summary>A movement of some item has not been costed yet.</summary>
    public bool CostPending { get; set; }
}

public sealed class InventoryItemSummaryReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    /// <summary>This page of items, by name.</summary>
    public List<InventoryItemSummaryItemDto> Items { get; set; } = [];

    /// <summary>How many items the filters match, all pages.</summary>
    public int TotalItems { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    /// <summary>Added up over every item the filters match, not only this page.</summary>
    public InventoryItemSummaryTotalsDto Totals { get; set; } = new();
}

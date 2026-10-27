using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// What the Inventory Item List (TK-245) is asked for: the as-at date, required (D-67);
/// optional filters for item category, status, item type, and whether to show only items with stock.
/// </summary>
public sealed class InventoryItemListReportRequest
{
    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsOfDate { get; set; }

    /// <summary>Only items in this category.</summary>
    public long? ItemCategoryId { get; set; }

    /// <summary>Status filter: Active, Inactive, or All/null.</summary>
    [MaxLength(20, ErrorMessage = "Status cannot exceed 20 characters.")]
    public string? Status { get; set; }

    /// <summary>Item type filter: Inventory, NonInventory, Service, or All/null.</summary>
    [MaxLength(30, ErrorMessage = "Item type cannot exceed 30 characters.")]
    public string? ItemType { get; set; }

    /// <summary>When true, only items where quantity on hand is non-zero.</summary>
    public bool OnlyWithStock { get; set; }

    /// <summary>Optional search string matching item code or name.</summary>
    [MaxLength(100, ErrorMessage = "Search query cannot exceed 100 characters.")]
    public string? Search { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 200, ErrorMessage = "Page size must be between 1 and 200.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>Result of the Inventory Item List report.</summary>
public sealed class InventoryItemListReportDto
{
    public DateOnly AsOfDate { get; set; }

    [MaxLength(10, ErrorMessage = "Base currency code cannot exceed 10 characters.")]
    public string BaseCurrencyCode { get; set; } = "INR";

    public IReadOnlyList<InventoryItemListItemDto> Items { get; set; } = [];

    public int TotalItems { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public InventoryItemListTotalsDto Totals { get; set; } = new();
}

/// <summary>One item row in the Inventory Item List.</summary>
public sealed class InventoryItemListItemDto
{
    public long ItemId { get; set; }

    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string ItemCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;

    public long? ItemCategoryId { get; set; }

    [MaxLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
    public string? CategoryName { get; set; }

    [MaxLength(50, ErrorMessage = "Item type cannot exceed 50 characters.")]
    public string ItemType { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Costing type cannot exceed 50 characters.")]
    public string CostingType { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "Status cannot exceed 20 characters.")]
    public string Status { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Unit code cannot exceed 50 characters.")]
    public string? UnitCode { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public decimal? PurchasePrice { get; set; }

    public decimal? SalesPrice { get; set; }

    public decimal? PurchaseTaxRate { get; set; }

    public decimal? SalesTaxRate { get; set; }

    [MaxLength(150, ErrorMessage = "Inventory account cannot exceed 150 characters.")]
    public string? InventoryAccount { get; set; }

    [MaxLength(150, ErrorMessage = "Purchase account cannot exceed 150 characters.")]
    public string? PurchaseAccount { get; set; }

    [MaxLength(150, ErrorMessage = "Sales account cannot exceed 150 characters.")]
    public string? SalesAccount { get; set; }

    public decimal QuantityOnHand { get; set; }

    public decimal AverageCost { get; set; }

    public decimal UnitCostPriceFifo { get; set; }

    public decimal TotalValue { get; set; }

    public decimal QuantityOnOrder { get; set; }

    public decimal QuantityCommitted { get; set; }

    public decimal BalanceQty { get; set; }

    public bool CostPending { get; set; }
}

/// <summary>Totals across all matching items for the Inventory Item List.</summary>
public sealed class InventoryItemListTotalsDto
{
    public decimal TotalQuantityOnHand { get; set; }

    public decimal TotalValue { get; set; }

    public decimal TotalQuantityOnOrder { get; set; }

    public decimal TotalQuantityCommitted { get; set; }

    public decimal TotalBalanceQty { get; set; }

    public bool CostPending { get; set; }
}

/// <summary>Item category choice for filter dropdown.</summary>
public sealed class InventoryItemListCategoryChoice
{
    public long ItemCategoryId { get; set; }

    [MaxLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
    public string CategoryName { get; set; } = string.Empty;
}

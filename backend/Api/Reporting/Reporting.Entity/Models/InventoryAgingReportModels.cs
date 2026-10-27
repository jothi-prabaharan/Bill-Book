using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// What the Inventory Aging Report (TK-246) is asked for: the as-at date, required (D-67);
/// optional category and warehouse filters, search, and paging.
/// </summary>
public sealed class InventoryAgingReportRequest
{
    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsOfDate { get; set; }

    /// <summary>Only items in this category.</summary>
    public long? ItemCategoryId { get; set; }

    /// <summary>Only stock held in this warehouse.</summary>
    public long? WarehouseId { get; set; }

    /// <summary>Optional search string matching item code or name.</summary>
    [MaxLength(100, ErrorMessage = "Search query cannot exceed 100 characters.")]
    public string? Search { get; set; }

    /// <summary>When true, only items with aged stock quantity > 0 are listed.</summary>
    public bool OnlyWithStock { get; set; } = true;

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 200, ErrorMessage = "Page size must be between 1 and 200.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>Quantity and value held within an aging bracket.</summary>
public sealed class InventoryAgingBucketDto
{
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
}

/// <summary>One item row in the Inventory Aging Report.</summary>
public sealed class InventoryAgingItemDto
{
    public long ItemId { get; set; }

    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string ItemCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;

    public long? ItemCategoryId { get; set; }

    [MaxLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
    public string? CategoryName { get; set; }

    [MaxLength(50, ErrorMessage = "Unit code cannot exceed 50 characters.")]
    public string? UnitCode { get; set; }

    [MaxLength(50, ErrorMessage = "Costing type cannot exceed 50 characters.")]
    public string CostingType { get; set; } = string.Empty;

    public decimal TotalQuantity { get; set; }

    public decimal TotalValue { get; set; }

    public InventoryAgingBucketDto Bucket0To30 { get; set; } = new();

    public InventoryAgingBucketDto Bucket31To60 { get; set; } = new();

    public InventoryAgingBucketDto Bucket61To90 { get; set; } = new();

    public InventoryAgingBucketDto Bucket91To180 { get; set; } = new();

    public InventoryAgingBucketDto BucketOver180 { get; set; } = new();
}

/// <summary>Totals across all items for each aging bucket.</summary>
public sealed class InventoryAgingTotalsDto
{
    public decimal TotalQuantity { get; set; }

    public decimal TotalValue { get; set; }

    public InventoryAgingBucketDto Bucket0To30 { get; set; } = new();

    public InventoryAgingBucketDto Bucket31To60 { get; set; } = new();

    public InventoryAgingBucketDto Bucket61To90 { get; set; } = new();

    public InventoryAgingBucketDto Bucket91To180 { get; set; } = new();

    public InventoryAgingBucketDto BucketOver180 { get; set; } = new();
}

/// <summary>Result of the Inventory Aging Report.</summary>
public sealed class InventoryAgingReportDto
{
    public DateOnly AsOfDate { get; set; }

    [MaxLength(10, ErrorMessage = "Base currency code cannot exceed 10 characters.")]
    public string BaseCurrencyCode { get; set; } = "INR";

    public IReadOnlyList<InventoryAgingItemDto> Items { get; set; } = [];

    public int TotalItems { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public InventoryAgingTotalsDto Totals { get; set; } = new();
}

/// <summary>Category choice for filter dropdown.</summary>
public sealed class InventoryAgingCategoryChoice
{
    public long ItemCategoryId { get; set; }

    [MaxLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
    public string CategoryName { get; set; } = string.Empty;
}

/// <summary>Warehouse choice for filter dropdown.</summary>
public sealed class InventoryAgingWarehouseChoice
{
    public long WarehouseId { get; set; }

    [MaxLength(50, ErrorMessage = "Warehouse code cannot exceed 50 characters.")]
    public string WarehouseCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Warehouse name cannot exceed 150 characters.")]
    public string WarehouseName { get; set; } = string.Empty;
}

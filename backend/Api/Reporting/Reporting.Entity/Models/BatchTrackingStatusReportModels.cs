using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// What the Batch Tracking Status Report (TK-247) asks for: the as-at date, required (D-67);
/// optional item, warehouse, expiry filters, search, and paging.
/// </summary>
public sealed class BatchTrackingStatusReportRequest
{
    [Required(ErrorMessage = "As at date is required.")]
    public DateOnly? AsOfDate { get; set; }

    /// <summary>Only batches for this item.</summary>
    public long? ItemId { get; set; }

    /// <summary>Only stock held in this warehouse.</summary>
    public long? WarehouseId { get; set; }

    /// <summary>Filter batches expiring within N days from AsOfDate.</summary>
    [Range(1, 3650, ErrorMessage = "Expiring within days must be between 1 and 3650.")]
    public int? ExpiringWithinDays { get; set; }

    /// <summary>When true, only batches whose ExpiryDate is before AsOfDate are returned.</summary>
    public bool ExpiredOnly { get; set; }

    /// <summary>When true, only batches with available quantity > 0 are listed.</summary>
    public bool WithStockOnly { get; set; } = true;

    /// <summary>Optional search matching batch number, item code or item name.</summary>
    [MaxLength(100, ErrorMessage = "Search query cannot exceed 100 characters.")]
    public string? Search { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 200, ErrorMessage = "Page size must be between 1 and 200.")]
    public int PageSize { get; set; } = 25;

    public Guid? OrgId { get; set; }
}

/// <summary>One batch row in the Batch Tracking Status Report.</summary>
public sealed class BatchTrackingStatusItemDto
{
    public long ItemBatchId { get; set; }

    [MaxLength(100, ErrorMessage = "Batch number cannot exceed 100 characters.")]
    public string BatchNumber { get; set; } = string.Empty;

    public long ItemId { get; set; }

    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string ItemCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
    public string? CategoryName { get; set; }

    [MaxLength(50, ErrorMessage = "Unit code cannot exceed 50 characters.")]
    public string? UnitCode { get; set; }

    public DateOnly? ManufactureDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [MaxLength(50, ErrorMessage = "Costing method cannot exceed 50 characters.")]
    public string CostingMethod { get; set; } = string.Empty;

    public long? WarehouseId { get; set; }

    [MaxLength(150, ErrorMessage = "Warehouse name cannot exceed 150 characters.")]
    public string WarehouseName { get; set; } = string.Empty;

    public decimal ReceivedQuantity { get; set; }

    public decimal AvailableQuantity { get; set; }

    public decimal? Mrp { get; set; }

    public int? DaysToExpiry { get; set; }

    public bool IsExpired { get; set; }

    public bool IsNearExpiry { get; set; }
}

/// <summary>Totals across all matched batches.</summary>
public sealed class BatchTrackingStatusTotalsDto
{
    public decimal TotalReceivedQuantity { get; set; }

    public decimal TotalAvailableQuantity { get; set; }

    public int ExpiredBatchCount { get; set; }

    public int NearExpiryBatchCount { get; set; }
}

/// <summary>Result of the Batch Tracking Status Report.</summary>
public sealed class BatchTrackingStatusReportDto
{
    public DateOnly AsOfDate { get; set; }

    public IReadOnlyList<BatchTrackingStatusItemDto> Items { get; set; } = [];

    public int TotalItems { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public BatchTrackingStatusTotalsDto Totals { get; set; } = new();
}

/// <summary>Item choice for filter dropdown.</summary>
public sealed class BatchTrackingItemChoice
{
    public long ItemId { get; set; }

    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string ItemCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;
}

/// <summary>Warehouse choice for filter dropdown.</summary>
public sealed class BatchTrackingWarehouseChoice
{
    public long WarehouseId { get; set; }

    [MaxLength(50, ErrorMessage = "Warehouse code cannot exceed 50 characters.")]
    public string WarehouseCode { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Warehouse name cannot exceed 150 characters.")]
    public string WarehouseName { get; set; } = string.Empty;
}

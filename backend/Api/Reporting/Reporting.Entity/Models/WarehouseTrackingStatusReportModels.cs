namespace Reporting.Entity.Models;

public sealed class WarehouseTrackingStatusReportRequest
{
    public DateOnly? AsOfDate { get; set; }
    public long? WarehouseId { get; set; }
    public long? ItemCategoryId { get; set; }
    public bool ShowZeroBalance { get; set; } = false;
    public string? Search { get; set; }
    public Guid? OrgId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class WarehouseTrackingItemDto
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? UnitCode { get; set; }
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal EstimatedValue { get; set; }
}

public sealed class WarehouseTrackingGroupDto
{
    public long WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalQuantityIn { get; set; }
    public decimal TotalQuantityOut { get; set; }
    public decimal TotalAvailableQuantity { get; set; }
    public decimal TotalEstimatedValue { get; set; }
    public List<WarehouseTrackingItemDto> Items { get; set; } = [];
}

public sealed class WarehouseTrackingTotalsDto
{
    public int TotalWarehouses { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalQuantityIn { get; set; }
    public decimal TotalQuantityOut { get; set; }
    public decimal TotalAvailableQuantity { get; set; }
    public decimal TotalEstimatedValue { get; set; }
}

public sealed class WarehouseTrackingStatusReportResultDto
{
    public DateOnly AsOfDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<WarehouseTrackingGroupDto> Warehouses { get; set; } = [];
    public WarehouseTrackingTotalsDto Totals { get; set; } = new();
    public int TotalWarehousesCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public sealed class WarehouseTrackingCategoryChoiceDto
{
    public long ItemCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

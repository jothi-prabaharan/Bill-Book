using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

public sealed class SerialTrackingStatusReportRequest
{
    public DateOnly? AsOfDate { get; set; }
    public long? ItemId { get; set; }
    public long? WarehouseId { get; set; }
    public SerialStatus? Status { get; set; }
    public string? Search { get; set; }
    public Guid? OrgId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class SerialTrackingStatusItemDto
{
    public long ItemSerialId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? UnitCode { get; set; }
    public string CostingMethod { get; set; } = string.Empty;
    public string? HallmarkNumber { get; set; }
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public DateOnly? ReceivedDate { get; set; }
    public string? ReceivedDocumentNumber { get; set; }
    public string? ReceivedFromContactName { get; set; }
    public DateOnly? SoldDate { get; set; }
    public string? SoldDocumentNumber { get; set; }
    public string? SoldToContactName { get; set; }
    public bool IsInStock { get; set; }
}

public sealed class SerialTrackingStatusTotalsDto
{
    public int TotalSerials { get; set; }
    public int InStockCount { get; set; }
    public int SoldCount { get; set; }
    public int ReturnedCount { get; set; }
    public int ScrappedCount { get; set; }
}

public sealed class SerialTrackingStatusReportResultDto
{
    public DateOnly AsOfDate { get; set; }
    public List<SerialTrackingStatusItemDto> Items { get; set; } = [];
    public SerialTrackingStatusTotalsDto Totals { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public sealed class SerialTrackingItemChoiceDto
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
}

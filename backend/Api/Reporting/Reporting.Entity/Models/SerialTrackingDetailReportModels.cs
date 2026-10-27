namespace Reporting.Entity.Models;

public sealed class SerialTrackingDetailReportRequest
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public long? ItemSerialId { get; set; }
    public long? ItemId { get; set; }
    public long? WarehouseId { get; set; }
    public long? ContactId { get; set; }
    public string? Search { get; set; }
    public Guid? OrgId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class SerialTrackingDetailMovementDto
{
    public long StockMovementId { get; set; }
    public DateOnly MovementDate { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public long? SourceId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? ContactName { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal RunningBalance { get; set; }
    public string? Notes { get; set; }
}

public sealed class SerialTrackingDetailGroupDto
{
    public long ItemSerialId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? HallmarkNumber { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? UnitCode { get; set; }
    public string CostingMethod { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal OpeningQuantity { get; set; }
    public decimal TotalIn { get; set; }
    public decimal TotalOut { get; set; }
    public decimal ClosingQuantity { get; set; }
    public List<SerialTrackingDetailMovementDto> Movements { get; set; } = [];
}

public sealed class SerialTrackingDetailTotalsDto
{
    public int TotalSerials { get; set; }
    public decimal TotalOpeningQuantity { get; set; }
    public decimal TotalInQuantity { get; set; }
    public decimal TotalOutQuantity { get; set; }
    public decimal TotalClosingQuantity { get; set; }
    public int TotalMovements { get; set; }
}

public sealed class SerialTrackingDetailReportResultDto
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<SerialTrackingDetailGroupDto> Serials { get; set; } = [];
    public SerialTrackingDetailTotalsDto Totals { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public sealed class SerialTrackingChoiceDto
{
    public long ItemSerialId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
}

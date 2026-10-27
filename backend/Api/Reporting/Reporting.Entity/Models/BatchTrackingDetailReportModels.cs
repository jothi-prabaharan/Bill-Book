namespace Reporting.Entity.Models;

public sealed class BatchTrackingDetailReportRequest
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public long? BatchId { get; set; }
    public long? ItemId { get; set; }
    public long? WarehouseId { get; set; }
    public long? ContactId { get; set; }
    public Guid? OrgId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class BatchTrackingDetailMovementDto
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
}

public sealed class BatchTrackingDetailGroupDto
{
    public long ItemBatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? UnitCode { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string CostingMethod { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public decimal OpeningQuantity { get; set; }
    public decimal TotalIn { get; set; }
    public decimal TotalOut { get; set; }
    public decimal ClosingQuantity { get; set; }
    public List<BatchTrackingDetailMovementDto> Movements { get; set; } = [];
}

public sealed class BatchTrackingDetailTotalsDto
{
    public int TotalBatches { get; set; }
    public decimal TotalOpeningQuantity { get; set; }
    public decimal TotalInQuantity { get; set; }
    public decimal TotalOutQuantity { get; set; }
    public decimal TotalClosingQuantity { get; set; }
    public int TotalMovements { get; set; }
}

public sealed class BatchTrackingDetailReportResultDto
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<BatchTrackingDetailGroupDto> Batches { get; set; } = [];
    public BatchTrackingDetailTotalsDto Totals { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public sealed class BatchTrackingBatchChoiceDto
{
    public long ItemBatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
}

public sealed class BatchTrackingContactChoiceDto
{
    public long ContactId { get; set; }
    public string ContactCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

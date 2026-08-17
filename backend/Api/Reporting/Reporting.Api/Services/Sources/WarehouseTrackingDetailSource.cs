using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class WarehouseTrackingDetailSource : ReportSource<WarehouseTrackingDetailRow>
{
    public override string ReportKey => "warehouse-tracking-detail";

    public override string Title => "Warehouse Tracking Detail Report";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override bool ForcesSortOrder => true;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
        new() { Name = "warehouseId", Label = "Warehouse", DataType = ColumnDataType.Number },
        new() { Name = "itemId", Label = "Item", DataType = ColumnDataType.Number },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<WarehouseTrackingDetailRow, string>("warehouseName", ColumnDataType.Text, r => r.WarehouseName, groupable: true),
        ReportColumn.Of<WarehouseTrackingDetailRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode, groupable: true),
        ReportColumn.Of<WarehouseTrackingDetailRow, string>("itemName", ColumnDataType.Text, r => r.ItemName, groupable: true),
        ReportColumn.Of<WarehouseTrackingDetailRow, string?>("batchOrSerialNo", ColumnDataType.Text, r => r.BatchOrSerialNo),
        ReportColumn.Of<WarehouseTrackingDetailRow, DateOnly>("transactionDate", ColumnDataType.Date, r => r.TransactionDate),
        
        ReportColumn.Of<WarehouseTrackingDetailRow, decimal>("quantityIn", ColumnDataType.Quantity, r => r.QuantityIn, AggregateFunction.Sum),
        ReportColumn.Of<WarehouseTrackingDetailRow, decimal>("quantityOut", ColumnDataType.Quantity, r => r.QuantityOut, AggregateFunction.Sum),
        ReportColumn.Of<WarehouseTrackingDetailRow, decimal>("balanceQuantity", ColumnDataType.Quantity, r => r.BalanceQuantity, sortable: false, filterable: false),
        
        ReportColumn.Of<WarehouseTrackingDetailRow, decimal>("trackedQuantity", ColumnDataType.Quantity, r => r.TrackedQuantity, AggregateFunction.Sum),
        ReportColumn.Of<WarehouseTrackingDetailRow, decimal>("unTrackedQuantity", ColumnDataType.Quantity, r => r.UnTrackedQuantity, AggregateFunction.Sum),
        ReportColumn.Of<WarehouseTrackingDetailRow, decimal>("totalQuantity", ColumnDataType.Quantity, r => r.TotalQuantity, AggregateFunction.Sum),
        
        ReportColumn.Of<WarehouseTrackingDetailRow, long>("stockMovementId", ColumnDataType.Number, r => r.StockMovementId, filterable: false),
    ];

    protected override IQueryable<WarehouseTrackingDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");
        long? warehouseId = parameters.Id("warehouseId");
        long? itemId = parameters.Id("itemId");

        IQueryable<Repository.ReadModels.StockMovementRead> movements = db.StockMovements;

        if (start is DateOnly from)
        {
            movements = movements.Where(m => m.MovementDate >= from);
        }

        if (end is DateOnly to)
        {
            movements = movements.Where(m => m.MovementDate <= to);
        }

        if (warehouseId is long whId)
        {
            movements = movements.Where(m => m.WarehouseId == whId);
        }
        
        if (itemId is long iId)
        {
            movements = movements.Where(m => m.ItemId == iId);
        }

        return from m in movements
               join w in db.Warehouses on m.WarehouseId equals w.WarehouseId
               join i in db.Items on m.ItemId equals i.ItemId
               join b in db.ItemBatches on m.ItemBatchId equals b.ItemBatchId into batches
               from b in batches.DefaultIfEmpty()
               select new WarehouseTrackingDetailRow
               {
                   StockMovementId = m.StockMovementId,
                   WarehouseName = w.WarehouseName,
                   ItemCode = i.ItemCode,
                   ItemName = i.ItemName,
                   BatchOrSerialNo = b != null ? b.BatchNumber : null,
                   TransactionDate = m.MovementDate,
                   
                   QuantityIn = m.Direction == 1 ? m.Quantity : 0m,
                   QuantityOut = m.Direction == 2 ? m.Quantity : 0m,
                   BalanceQuantity = 0m,
                   
                   TrackedQuantity = m.ItemBatchId != null ? (m.Direction == 1 ? m.Quantity : -m.Quantity) : 0m,
                   UnTrackedQuantity = m.ItemBatchId == null ? (m.Direction == 1 ? m.Quantity : -m.Quantity) : 0m,
                   TotalQuantity = m.Direction == 1 ? m.Quantity : -m.Quantity
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<WarehouseTrackingDetailRow, long>>)(r => r.StockMovementId);

    protected override async Task<decimal?> OpeningBalanceAsync(
        IQueryable<WarehouseTrackingDetailRow> rowsBeforeThisPage, CancellationToken ct) =>
        await rowsBeforeThisPage.SumAsync(r => r.QuantityIn - r.QuantityOut, ct);

    protected override void ApplyRunningTotals(
        IReadOnlyList<WarehouseTrackingDetailRow> page, decimal opening)
    {
        decimal balance = opening;

        foreach (WarehouseTrackingDetailRow row in page)
        {
            balance += row.QuantityIn - row.QuantityOut;
            row.BalanceQuantity = balance;
        }
    }
}

public sealed class WarehouseTrackingDetailRow
{
    public long StockMovementId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? BatchOrSerialNo { get; set; }
    public DateOnly TransactionDate { get; set; }
    
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal BalanceQuantity { get; set; }
    
    public decimal TrackedQuantity { get; set; }
    public decimal UnTrackedQuantity { get; set; }
    public decimal TotalQuantity { get; set; }
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public sealed class BatchTrackingStatusSourceRepository : ReportSource<BatchTrackingStatusRow>
{
    public override string ReportKey => "batch-tracking-status";

    public override string Title => "Batch Tracking Status Report";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "asOf", Label = "As Of", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BatchTrackingStatusRow, string>("batchNo", ColumnDataType.Text, r => r.BatchNo),
        ReportColumn.Of<BatchTrackingStatusRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<BatchTrackingStatusRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<BatchTrackingStatusRow, string?>("description", ColumnDataType.Text, r => r.Description),
        ReportColumn.Of<BatchTrackingStatusRow, string?>("productCategory", ColumnDataType.Text, r => r.ProductCategory, groupable: true),
        ReportColumn.Of<BatchTrackingStatusRow, DateOnly?>("manufacturedDate", ColumnDataType.Date, r => r.ManufacturedDate),
        ReportColumn.Of<BatchTrackingStatusRow, DateOnly?>("expiryDate", ColumnDataType.Date, r => r.ExpiryDate),
        ReportColumn.Of<BatchTrackingStatusRow, decimal>("batchQuantity", ColumnDataType.Quantity, r => r.BatchQuantity, AggregateFunction.Sum),
        ReportColumn.Of<BatchTrackingStatusRow, decimal>("availableQuantity", ColumnDataType.Quantity, r => r.AvailableQuantity, AggregateFunction.Sum),
        ReportColumn.Of<BatchTrackingStatusRow, string>("costingMethod", ColumnDataType.Enum, r => r.CostingMethod, groupable: true),
        ReportColumn.Of<BatchTrackingStatusRow, string>("warehouse", ColumnDataType.Text, r => r.Warehouse, groupable: true),
        ReportColumn.Of<BatchTrackingStatusRow, long>("itemBatchId", ColumnDataType.Number, r => r.ItemBatchId, filterable: false),
    ];

    protected override IQueryable<BatchTrackingStatusRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly asOf = parameters.Date("asOf") ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var batchMoves = db.StockMovements
            .Where(m => m.ItemBatchId != null && m.MovementDate <= asOf)
            .GroupBy(m => new { ItemBatchId = m.ItemBatchId!.Value, m.WarehouseId })
            .Select(g => new
            {
                g.Key.ItemBatchId,
                g.Key.WarehouseId,
                BatchQuantity = g.Where(x => x.Direction == StockDirection.In).Sum(x => x.Quantity),
                AvailableQuantity = g.Sum(x => x.Direction == StockDirection.In ? x.Quantity : -x.Quantity)
            });

        return from m in batchMoves
               join b in db.ItemBatches on m.ItemBatchId equals b.ItemBatchId
               join i in db.Items on b.ItemId equals i.ItemId
               join c in db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
               from c in categories.DefaultIfEmpty()
               join w in db.Warehouses on m.WarehouseId equals w.WarehouseId into warehouses
               from w in warehouses.DefaultIfEmpty()
               select new BatchTrackingStatusRow
               {
                   ItemBatchId = b.ItemBatchId,
                   BatchNo = b.BatchNumber,
                   ItemCode = i.ItemCode,
                   ItemName = i.ItemName,
                   Description = i.Description,
                   ProductCategory = c != null ? c.CategoryName : null,
                   ManufacturedDate = b.ManufactureDate,
                   ExpiryDate = b.ExpiryDate,
                   BatchQuantity = m.BatchQuantity,
                   AvailableQuantity = m.AvailableQuantity,
                   CostingMethod = i.CostingType.ToString(),
                   Warehouse = w != null ? w.WarehouseName : "Unknown"
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BatchTrackingStatusRow, long>>)(r => r.ItemBatchId);
}

public sealed class BatchTrackingStatusRow
{
    public long ItemBatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public string? ProductCategory { get; set; }
    public DateOnly? ManufacturedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public decimal BatchQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public string CostingMethod { get; set; } = null!;
    public string Warehouse { get; set; } = null!;
}

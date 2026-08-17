using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class BatchTrackingStatusSource : ReportSource<BatchTrackingStatusRow>
{
    public override string ReportKey => "batch-tracking-status";

    public override string Title => "Batch Tracking Status Report";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

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
        return from b in db.ItemBatches
               join i in db.Items on b.ItemId equals i.ItemId
               join c in db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
               from c in categories.DefaultIfEmpty()
               // Group movements by batch and warehouse to get quantities per warehouse
               let moves = db.StockMovements.Where(m => m.ItemBatchId == b.ItemBatchId)
               // Instead of a complex cross apply, we can just flatten or group.
               // Actually, a batch could be in multiple warehouses, but REPORTS.md says:
               // "Batch No, Item Code... Warehouse". It expects one row per Batch per Warehouse.
               // EF Core grouping on multiple keys translates well:
               from g in db.StockMovements
                    .Where(m => m.ItemBatchId == b.ItemBatchId)
                    .GroupBy(m => m.WarehouseId)
               join w in db.Warehouses on g.Key equals w.WarehouseId into warehouses
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
                   BatchQuantity = g.Where(x => x.Direction == 1).Sum(x => x.Quantity),
                   AvailableQuantity = g.Sum(x => x.Direction == 1 ? x.Quantity : -x.Quantity),
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

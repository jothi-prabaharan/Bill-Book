using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class BatchTrackingDetailSource : ReportSource<BatchTrackingDetailRow>
{
    public override string ReportKey => "batch-tracking-detail";

    public override string Title => "Batch Tracking Detail Report";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BatchTrackingDetailRow, string>("batchNo", ColumnDataType.Text, r => r.BatchNo, groupable: true),
        ReportColumn.Of<BatchTrackingDetailRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<BatchTrackingDetailRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<BatchTrackingDetailRow, string?>("description", ColumnDataType.Text, r => r.Description),
        ReportColumn.Of<BatchTrackingDetailRow, string?>("productCategory", ColumnDataType.Text, r => r.ProductCategory, groupable: true),
        ReportColumn.Of<BatchTrackingDetailRow, DateOnly?>("manufacturedDate", ColumnDataType.Date, r => r.ManufacturedDate),
        ReportColumn.Of<BatchTrackingDetailRow, DateOnly?>("expiryDate", ColumnDataType.Date, r => r.ExpiryDate),
        ReportColumn.Of<BatchTrackingDetailRow, string>("costingMethod", ColumnDataType.Enum, r => r.CostingMethod, groupable: true),
        ReportColumn.Of<BatchTrackingDetailRow, string>("warehouse", ColumnDataType.Text, r => r.Warehouse, groupable: true),
        
        ReportColumn.Of<BatchTrackingDetailRow, DateOnly>("transactionDate", ColumnDataType.Date, r => r.TransactionDate),
        ReportColumn.Of<BatchTrackingDetailRow, string?>("transactionNo", ColumnDataType.Text, r => r.TransactionNo),
        ReportColumn.Of<BatchTrackingDetailRow, string>("transactionType", ColumnDataType.Enum, r => r.TransactionType, groupable: true),
        ReportColumn.Of<BatchTrackingDetailRow, string?>("contactName", ColumnDataType.Text, r => r.ContactName),
        ReportColumn.Of<BatchTrackingDetailRow, decimal>("quantityIn", ColumnDataType.Quantity, r => r.QuantityIn, AggregateFunction.Sum),
        ReportColumn.Of<BatchTrackingDetailRow, decimal>("quantityOut", ColumnDataType.Quantity, r => r.QuantityOut, AggregateFunction.Sum),
        ReportColumn.Of<BatchTrackingDetailRow, string>("unitOfMeasurement", ColumnDataType.Text, r => r.UnitOfMeasurement, groupable: true),

        ReportColumn.Of<BatchTrackingDetailRow, long>("stockMovementId", ColumnDataType.Number, r => r.StockMovementId, filterable: false),
    ];

    protected override IQueryable<BatchTrackingDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        IQueryable<Repository.ReadModels.StockMovementRead> movements = db.StockMovements.Where(m => m.ItemBatchId != null);

        if (start is DateOnly from)
        {
            movements = movements.Where(m => m.MovementDate >= from);
        }

        if (end is DateOnly to)
        {
            movements = movements.Where(m => m.MovementDate <= to);
        }

        return from m in movements
               join b in db.ItemBatches on m.ItemBatchId equals b.ItemBatchId
               join i in db.Items on b.ItemId equals i.ItemId
               join c in db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
               from c in categories.DefaultIfEmpty()
               join u in db.UnitsOfMeasure on i.InventoryUomId equals u.UomId into uoms
               from u in uoms.DefaultIfEmpty()
               join w in db.Warehouses on m.WarehouseId equals w.WarehouseId into warehouses
               from w in warehouses.DefaultIfEmpty()
               select new BatchTrackingDetailRow
               {
                   StockMovementId = m.StockMovementId,
                   BatchNo = b.BatchNumber,
                   ItemCode = i.ItemCode,
                   ItemName = i.ItemName,
                   Description = i.Description,
                   ProductCategory = c != null ? c.CategoryName : null,
                   ManufacturedDate = b.ManufactureDate,
                   ExpiryDate = b.ExpiryDate,
                   CostingMethod = i.CostingType.ToString(),
                   Warehouse = w != null ? w.WarehouseName : "Unknown",

                   TransactionDate = m.MovementDate,
                   TransactionNo = m.SourceId != null ? m.SourceType + "-" + m.SourceId : null,
                   TransactionType = m.MovementType.ToString(),
                   ContactName = null, // Will be filled by batched resolver if needed, or by source doc join
                   QuantityIn = m.Direction == 1 ? m.Quantity : 0m,
                   QuantityOut = m.Direction == 2 ? m.Quantity : 0m,
                   UnitOfMeasurement = u != null ? u.UomName : "Unknown"
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BatchTrackingDetailRow, long>>)(r => r.StockMovementId);
}

public sealed class BatchTrackingDetailRow
{
    public long StockMovementId { get; set; }
    public string BatchNo { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public string? ProductCategory { get; set; }
    public DateOnly? ManufacturedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string CostingMethod { get; set; } = null!;
    public string Warehouse { get; set; } = null!;
    public DateOnly TransactionDate { get; set; }
    public string? TransactionNo { get; set; }
    public string TransactionType { get; set; } = null!;
    public string? ContactName { get; set; }
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public string UnitOfMeasurement { get; set; } = null!;
}

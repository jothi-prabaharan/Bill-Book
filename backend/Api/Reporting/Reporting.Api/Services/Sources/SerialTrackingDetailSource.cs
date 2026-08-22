using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Api.Services.Sources;

public sealed class SerialTrackingDetailSource : ReportSource<SerialTrackingDetailRow>
{
    public override string ReportKey => "serial-tracking-detail";

    public override string Title => "Serial Tracking Detail Report";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SerialTrackingDetailRow, string>("serialNo", ColumnDataType.Text, r => r.SerialNo, groupable: true),
        ReportColumn.Of<SerialTrackingDetailRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<SerialTrackingDetailRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<SerialTrackingDetailRow, string?>("productCategory", ColumnDataType.Text, r => r.ProductCategory, groupable: true),
        ReportColumn.Of<SerialTrackingDetailRow, DateOnly?>("manufacturedDate", ColumnDataType.Date, r => r.ManufacturedDate),
        ReportColumn.Of<SerialTrackingDetailRow, DateOnly?>("expiryDate", ColumnDataType.Date, r => r.ExpiryDate),
        ReportColumn.Of<SerialTrackingDetailRow, string>("costingMethod", ColumnDataType.Enum, r => r.CostingMethod, groupable: true),
        ReportColumn.Of<SerialTrackingDetailRow, string>("warehouse", ColumnDataType.Text, r => r.Warehouse, groupable: true),
        
        ReportColumn.Of<SerialTrackingDetailRow, DateOnly>("transactionDate", ColumnDataType.Date, r => r.TransactionDate),
        ReportColumn.Of<SerialTrackingDetailRow, string?>("transactionNo", ColumnDataType.Text, r => r.TransactionNo),
        ReportColumn.Of<SerialTrackingDetailRow, string>("transactionType", ColumnDataType.Enum, r => r.TransactionType, groupable: true),
        ReportColumn.Of<SerialTrackingDetailRow, string?>("contactName", ColumnDataType.Text, r => r.ContactName),
        ReportColumn.Of<SerialTrackingDetailRow, decimal>("quantityIn", ColumnDataType.Quantity, r => r.QuantityIn, AggregateFunction.Sum),
        ReportColumn.Of<SerialTrackingDetailRow, decimal>("quantityOut", ColumnDataType.Quantity, r => r.QuantityOut, AggregateFunction.Sum),
        ReportColumn.Of<SerialTrackingDetailRow, string>("unitOfMeasurement", ColumnDataType.Text, r => r.UnitOfMeasurement, groupable: true),

        ReportColumn.Of<SerialTrackingDetailRow, long>("stockMovementId", ColumnDataType.Number, r => r.StockMovementId, filterable: false),
    ];

    protected override IQueryable<SerialTrackingDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        var received = from m in db.StockMovements
                       join s in db.ItemSerials on m.StockMovementId equals s.ReceivedMovementId
                       select new { m, s };

        var issued = from m in db.StockMovements
                     join s in db.ItemSerials on m.StockMovementId equals s.IssuedMovementId
                     select new { m, s };

        var combined = received.Concat(issued);

        if (start is DateOnly fromDate)
        {
            combined = combined.Where(x => x.m.MovementDate >= fromDate);
        }

        if (end is DateOnly toDate)
        {
            combined = combined.Where(x => x.m.MovementDate <= toDate);
        }

        return from c in combined
               join i in db.Items on c.s.ItemId equals i.ItemId
               join cat in db.ItemCategories on i.ItemCategoryId equals cat.ItemCategoryId into categories
               from cat in categories.DefaultIfEmpty()
               join u in db.UnitsOfMeasure on i.InventoryUomId equals u.UomId into uoms
               from u in uoms.DefaultIfEmpty()
               join b in db.ItemBatches on c.s.ItemBatchId equals b.ItemBatchId into batches
               from b in batches.DefaultIfEmpty()
               join w in db.Warehouses on c.s.WarehouseId equals w.WarehouseId into warehouses
               from w in warehouses.DefaultIfEmpty()
               select new SerialTrackingDetailRow
               {
                   StockMovementId = c.m.StockMovementId,
                   SerialNo = c.s.SerialNumber,
                   ItemCode = i.ItemCode,
                   ItemName = i.ItemName,
                   ProductCategory = cat != null ? cat.CategoryName : null,
                   ManufacturedDate = b != null ? b.ManufactureDate : null,
                   ExpiryDate = b != null ? b.ExpiryDate : null,
                   CostingMethod = i.CostingType.ToString(),
                   Warehouse = w != null ? w.WarehouseName : "Unknown",

                   TransactionDate = c.m.MovementDate,
                   TransactionNo = c.m.SourceId != null ? c.m.SourceType + "-" + c.m.SourceId : null,
                   TransactionType = c.m.MovementType.ToString(),
                   ContactName = null, // Set via resolver if necessary
                   QuantityIn = c.m.Direction == 1 ? 1m : 0m,
                   QuantityOut = c.m.Direction == 2 ? 1m : 0m,
                   UnitOfMeasurement = u != null ? u.UomName : "Unknown"
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SerialTrackingDetailRow, long>>)(r => r.StockMovementId);
}

public sealed class SerialTrackingDetailRow
{
    public long StockMovementId { get; set; }
    public string SerialNo { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
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

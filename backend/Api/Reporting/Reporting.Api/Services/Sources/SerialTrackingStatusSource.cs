using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class SerialTrackingStatusSource : ReportSource<SerialTrackingStatusRow>
{
    public override string ReportKey => "serial-tracking-status";

    public override string Title => "Serial Tracking Status Report";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SerialTrackingStatusRow, string>("serialNo", ColumnDataType.Text, r => r.SerialNo),
        ReportColumn.Of<SerialTrackingStatusRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<SerialTrackingStatusRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<SerialTrackingStatusRow, string?>("productCategory", ColumnDataType.Text, r => r.ProductCategory, groupable: true),
        ReportColumn.Of<SerialTrackingStatusRow, DateOnly?>("manufacturedDate", ColumnDataType.Date, r => r.ManufacturedDate),
        ReportColumn.Of<SerialTrackingStatusRow, DateOnly?>("expiryDate", ColumnDataType.Date, r => r.ExpiryDate),
        ReportColumn.Of<SerialTrackingStatusRow, decimal>("availableQuantity", ColumnDataType.Quantity, r => r.AvailableQuantity, AggregateFunction.Sum),
        ReportColumn.Of<SerialTrackingStatusRow, string>("costingMethod", ColumnDataType.Enum, r => r.CostingMethod, groupable: true),
        ReportColumn.Of<SerialTrackingStatusRow, string>("warehouse", ColumnDataType.Text, r => r.Warehouse, groupable: true),
        ReportColumn.Of<SerialTrackingStatusRow, long>("itemSerialId", ColumnDataType.Number, r => r.ItemSerialId, filterable: false),
    ];

    protected override IQueryable<SerialTrackingStatusRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from s in db.ItemSerials
               join i in db.Items on s.ItemId equals i.ItemId
               join c in db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
               from c in categories.DefaultIfEmpty()
               join b in db.ItemBatches on s.ItemBatchId equals b.ItemBatchId into batches
               from b in batches.DefaultIfEmpty()
               join w in db.Warehouses on s.WarehouseId equals w.WarehouseId into warehouses
               from w in warehouses.DefaultIfEmpty()
               // InStock = 0, Sold = 1, Returned = 2, Scrapped = 3
               let isAvailable = s.Status == 0 || s.Status == 2
               select new SerialTrackingStatusRow
               {
                   ItemSerialId = s.ItemSerialId,
                   SerialNo = s.SerialNumber,
                   ItemCode = i.ItemCode,
                   ItemName = i.ItemName,
                   ProductCategory = c != null ? c.CategoryName : null,
                   ManufacturedDate = b != null ? b.ManufactureDate : null,
                   ExpiryDate = b != null ? b.ExpiryDate : null,
                   AvailableQuantity = isAvailable ? 1m : 0m,
                   CostingMethod = i.CostingType.ToString(),
                   Warehouse = w != null ? w.WarehouseName : "Unknown"
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SerialTrackingStatusRow, long>>)(r => r.ItemSerialId);
}

public sealed class SerialTrackingStatusRow
{
    public long ItemSerialId { get; set; }
    public string SerialNo { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? ProductCategory { get; set; }
    public DateOnly? ManufacturedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public decimal AvailableQuantity { get; set; }
    public string CostingMethod { get; set; } = null!;
    public string Warehouse { get; set; } = null!;
}

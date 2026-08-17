using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class InventoryAgingSource : ReportSource<InventoryAgingRow>
{
    public override string ReportKey => "inventory-aging";

    public override string Title => "Inventory Aging Report";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "asOf", Label = "As Of", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<InventoryAgingRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<InventoryAgingRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<InventoryAgingRow, string?>("productCategory", ColumnDataType.Text, r => r.ProductCategory, groupable: true),
        ReportColumn.Of<InventoryAgingRow, string>("unitOfMeasurement", ColumnDataType.Text, r => r.UnitOfMeasurement, groupable: true),
        ReportColumn.Of<InventoryAgingRow, string?>("inventoryAssetAccount", ColumnDataType.Text, r => r.InventoryAssetAccount),
        ReportColumn.Of<InventoryAgingRow, string>("bucket", ColumnDataType.Text, r => r.Bucket, groupable: true),
        ReportColumn.Of<InventoryAgingRow, decimal>("agedInventoryQuantity", ColumnDataType.Quantity, r => r.AgedInventoryQuantity, AggregateFunction.Sum),
        ReportColumn.Of<InventoryAgingRow, decimal>("agedInventoryValue", ColumnDataType.Money, r => r.AgedInventoryValue, AggregateFunction.Sum),
        ReportColumn.Of<InventoryAgingRow, long>("costLayerId", ColumnDataType.Number, r => r.CostLayerId, filterable: false),
    ];

    protected override IQueryable<InventoryAgingRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly asOf = parameters.Date("asOf") ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return from l in db.CostLayers
               where l.RemainingQuantity > 0
               join i in db.Items on l.ItemId equals i.ItemId
               join c in db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
               from c in categories.DefaultIfEmpty()
               join u in db.UnitsOfMeasure on i.InventoryUomId equals u.UomId into uoms
               from u in uoms.DefaultIfEmpty()
               // Use standard bucket boundaries in SQL using received date difference
               // We approximate days using exact dates, since EF.Functions in PG might be tricky
               let diff = asOf.DayNumber - l.ReceivedOn.DayNumber
               select new InventoryAgingRow
               {
                   CostLayerId = l.CostLayerId,
                   ItemCode = i.ItemCode,
                   ItemName = i.ItemName,
                   ProductCategory = c != null ? c.CategoryName : null,
                   UnitOfMeasurement = u != null ? u.UomName : "Unknown",
                   InventoryAssetAccount = null,
                   Bucket = diff <= 30 ? "0-30 Days" :
                            diff <= 60 ? "31-60 Days" :
                            diff <= 90 ? "61-90 Days" :
                            "91+ Days",
                   AgedInventoryQuantity = l.RemainingQuantity,
                   AgedInventoryValue = l.RemainingQuantity * l.UnitCost
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<InventoryAgingRow, long>>)(r => r.CostLayerId);
}

public sealed class InventoryAgingRow
{
    public long CostLayerId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? ProductCategory { get; set; }
    public string UnitOfMeasurement { get; set; } = null!;
    public string? InventoryAssetAccount { get; set; }
    public string Bucket { get; set; } = null!;
    public decimal AgedInventoryQuantity { get; set; }
    public decimal AgedInventoryValue { get; set; }
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class WarehouseTrackingStatusSource : ReportSource<WarehouseTrackingStatusRow>
{
    public override string ReportKey => "warehouse-tracking-status";

    public override string Title => "Warehouse Tracking Status Report";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<WarehouseTrackingStatusRow, string>("warehouseName", ColumnDataType.Text, r => r.WarehouseName),
        ReportColumn.Of<WarehouseTrackingStatusRow, string?>("address", ColumnDataType.Text, r => r.Address),
        ReportColumn.Of<WarehouseTrackingStatusRow, string?>("city", ColumnDataType.Text, r => r.City, groupable: true),
        ReportColumn.Of<WarehouseTrackingStatusRow, int?>("state", ColumnDataType.Number, r => r.StateId, groupable: true),
        ReportColumn.Of<WarehouseTrackingStatusRow, int?>("country", ColumnDataType.Number, r => r.CountryId, groupable: true),
        ReportColumn.Of<WarehouseTrackingStatusRow, bool>("primary", ColumnDataType.Enum, r => r.Primary),
        ReportColumn.Of<WarehouseTrackingStatusRow, bool>("status", ColumnDataType.Enum, r => r.Status),
        ReportColumn.Of<WarehouseTrackingStatusRow, decimal>("quantityIn", ColumnDataType.Quantity, r => r.QuantityIn, AggregateFunction.Sum),
        ReportColumn.Of<WarehouseTrackingStatusRow, decimal>("quantityOut", ColumnDataType.Quantity, r => r.QuantityOut, AggregateFunction.Sum),
        ReportColumn.Of<WarehouseTrackingStatusRow, decimal>("availableQuantity", ColumnDataType.Quantity, r => r.AvailableQuantity, AggregateFunction.Sum),
        ReportColumn.Of<WarehouseTrackingStatusRow, long>("warehouseId", ColumnDataType.Number, r => r.WarehouseId, filterable: false),
    ];

    protected override IQueryable<WarehouseTrackingStatusRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from w in db.Warehouses
               let inQty = db.StockMovements.Where(m => m.WarehouseId == w.WarehouseId && m.Direction == 1).Sum(m => (decimal?)m.Quantity) ?? 0m
               let outQty = db.StockMovements.Where(m => m.WarehouseId == w.WarehouseId && m.Direction == 2).Sum(m => (decimal?)m.Quantity) ?? 0m
               select new WarehouseTrackingStatusRow
               {
                   WarehouseId = w.WarehouseId,
                   WarehouseName = w.WarehouseName,
                   Address = w.AddressLine1,
                   City = w.City,
                   StateId = w.StateId,
                   CountryId = w.CountryId,
                   Primary = w.IsDefault,
                   Status = w.IsActive,
                   QuantityIn = inQty,
                   QuantityOut = outQty,
                   AvailableQuantity = inQty - outQty
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<WarehouseTrackingStatusRow, long>>)(r => r.WarehouseId);
}

public sealed class WarehouseTrackingStatusRow
{
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public string? Address { get; set; }
    public string? City { get; set; }
    public int? StateId { get; set; }
    public int? CountryId { get; set; }
    public bool Primary { get; set; }
    public bool Status { get; set; }
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal AvailableQuantity { get; set; }
}

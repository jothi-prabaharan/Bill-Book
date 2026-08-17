using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class ItemListSource : ReportSource<ItemListRow>
{
    private readonly BatchedNameResolver _resolver;

    public ItemListSource(BatchedNameResolver resolver)
    {
        _resolver = resolver;
    }

    public override string ReportKey => "inventory-item-list";

    public override string Title => "Inventory Item List";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ItemListRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<ItemListRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<ItemListRow, string?>("itemGroup", ColumnDataType.Text, r => r.ItemGroup, groupable: true),
        ReportColumn.Of<ItemListRow, string?>("productCategory", ColumnDataType.Text, r => r.ProductCategory, groupable: true),
        ReportColumn.Of<ItemListRow, string>("inventoryType", ColumnDataType.Enum, r => r.InventoryType, groupable: true),
        ReportColumn.Of<ItemListRow, string>("costingMethod", ColumnDataType.Enum, r => r.CostingMethod, groupable: true),
        ReportColumn.Of<ItemListRow, string>("status", ColumnDataType.Enum, r => r.Status, groupable: true),
        ReportColumn.Of<ItemListRow, DateOnly?>("date", ColumnDataType.Date, r => r.Date),
        // Organization is implicit in the branch, but we can add it if needed. 
        ReportColumn.Of<ItemListRow, string>("unitOfMeasurement", ColumnDataType.Text, r => r.UnitOfMeasurement, groupable: true),
        ReportColumn.Of<ItemListRow, string?>("purchaseDescription", ColumnDataType.Text, r => r.PurchaseDescription),
        ReportColumn.Of<ItemListRow, string?>("salesDescription", ColumnDataType.Text, r => r.SalesDescription),
        ReportColumn.Of<ItemListRow, string?>("purchaseTaxRate", ColumnDataType.Text, r => r.PurchaseTaxRate),
        ReportColumn.Of<ItemListRow, string?>("salesTaxRate", ColumnDataType.Text, r => r.SalesTaxRate),
        ReportColumn.Of<ItemListRow, string?>("inventoryAccount", ColumnDataType.Text, r => r.InventoryAccount),
        ReportColumn.Of<ItemListRow, string?>("purchaseAccount", ColumnDataType.Text, r => r.PurchaseAccount),
        ReportColumn.Of<ItemListRow, string?>("salesAccount", ColumnDataType.Text, r => r.SalesAccount),
        ReportColumn.Of<ItemListRow, decimal?>("quantityOnHand", ColumnDataType.Quantity, r => r.QuantityOnHand, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("averageCost", ColumnDataType.Money, r => r.AverageCost),
        ReportColumn.Of<ItemListRow, decimal?>("unitCostPrice", ColumnDataType.Money, r => r.UnitCostPrice),
        ReportColumn.Of<ItemListRow, decimal?>("unitSalePrice", ColumnDataType.Money, r => r.UnitSalePrice),
        ReportColumn.Of<ItemListRow, decimal?>("totalValue", ColumnDataType.Money, r => r.TotalValue, AggregateFunction.Sum),

        // Partial - to be filled later by sal/pur
        ReportColumn.Of<ItemListRow, decimal?>("quantityOnOrder", ColumnDataType.Quantity, r => r.QuantityOnOrder, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("quantityReceived", ColumnDataType.Quantity, r => r.QuantityReceived, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("committedQuotes", ColumnDataType.Quantity, r => r.CommittedQuotes, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("committedToDO", ColumnDataType.Quantity, r => r.CommittedToDO, AggregateFunction.Sum),

        ReportColumn.Of<ItemListRow, long>("itemId", ColumnDataType.Number, r => r.ItemId, filterable: false),
    ];

    protected override IQueryable<ItemListRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from i in db.Items
               join s in db.ItemStocks on i.ItemId equals s.ItemId into stocks
               from s in stocks.DefaultIfEmpty()
               join c in db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
               from c in categories.DefaultIfEmpty()
               join u in db.UnitsOfMeasure on i.InventoryUomId equals u.UomId into uoms
               from u in uoms.DefaultIfEmpty()
               select new ItemListRow
               {
                   ItemId = i.ItemId,
                   ItemCode = i.ItemCode,
                   ItemName = i.ItemName,
                   ItemGroup = c != null ? c.CategoryName : null,
                   ProductCategory = c != null ? c.CategoryName : null,
                   InventoryType = i.ItemType.ToString(),
                   CostingMethod = i.CostingType.ToString(),
                   Status = i.IsActive ? "Active" : "Inactive",
                   Date = null, // Or from DateOnly.FromDateTime(i.CreatedAt) if available
                   UnitOfMeasurement = u != null ? u.UomName : "Unknown",
                   PurchaseDescription = i.Description,
                   SalesDescription = i.Description,
                   QuantityOnHand = s != null ? s.QuantityOnHand : 0m,
                   AverageCost = s != null ? s.WeightedAverageCost : 0m,
                   UnitCostPrice = i.PurchasePrice,
                   UnitSalePrice = i.SalesPrice,
                   TotalValue = s != null ? (s.QuantityOnHand * s.WeightedAverageCost) : 0m,
                   QuantityOnOrder = null,
                   QuantityReceived = null,
                   CommittedQuotes = null,
                   CommittedToDO = null
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ItemListRow, long>>)(r => r.ItemId);
}

public sealed class ItemListRow
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? ItemGroup { get; set; }
    public string? ProductCategory { get; set; }
    public string InventoryType { get; set; } = null!;
    public string CostingMethod { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateOnly? Date { get; set; }
    public string UnitOfMeasurement { get; set; } = null!;
    public string? PurchaseDescription { get; set; }
    public string? SalesDescription { get; set; }
    public string? PurchaseTaxRate { get; set; }
    public string? SalesTaxRate { get; set; }
    public string? InventoryAccount { get; set; }
    public string? PurchaseAccount { get; set; }
    public string? SalesAccount { get; set; }
    public decimal? QuantityOnHand { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal? UnitCostPrice { get; set; }
    public decimal? UnitSalePrice { get; set; }
    public decimal? TotalValue { get; set; }
    public decimal? QuantityOnOrder { get; set; }
    public decimal? QuantityReceived { get; set; }
    public decimal? CommittedQuotes { get; set; }
    public decimal? CommittedToDO { get; set; }
}

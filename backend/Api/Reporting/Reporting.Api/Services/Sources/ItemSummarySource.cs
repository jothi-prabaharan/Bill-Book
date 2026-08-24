using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class ItemSummarySource : ReportSource<ItemSummaryRow>
{
    public override string ReportKey => "inventory-item-summary";

    public override string Title => "Inventory Item Summary";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ItemSummaryRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<ItemSummaryRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<ItemSummaryRow, string?>("itemGroup", ColumnDataType.Text, r => r.ItemGroup, groupable: true),
        ReportColumn.Of<ItemSummaryRow, string?>("productCategory", ColumnDataType.Text, r => r.ProductCategory, groupable: true),
        ReportColumn.Of<ItemSummaryRow, string>("inventoryType", ColumnDataType.Enum, r => r.InventoryType, groupable: true),
        ReportColumn.Of<ItemSummaryRow, string>("costingMethod", ColumnDataType.Enum, r => r.CostingMethod, groupable: true),
        ReportColumn.Of<ItemSummaryRow, string>("unitOfMeasurement", ColumnDataType.Text, r => r.UnitOfMeasurement, groupable: true),
        ReportColumn.Of<ItemSummaryRow, decimal>("openingQuantity", ColumnDataType.Quantity, r => r.OpeningQuantity, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal>("openingBalance", ColumnDataType.Money, r => r.OpeningBalance, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal?>("quantityPurchased", ColumnDataType.Quantity, r => r.QuantityPurchased, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal?>("purchases", ColumnDataType.Money, r => r.Purchases, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal?>("quantitySold", ColumnDataType.Quantity, r => r.QuantitySold, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal?>("sales", ColumnDataType.Money, r => r.Sales, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal>("quantityAdjusted", ColumnDataType.Quantity, r => r.QuantityAdjusted, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal>("adjustments", ColumnDataType.Money, r => r.Adjustments, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal?>("cogs", ColumnDataType.Money, r => r.Cogs, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal?>("profit", ColumnDataType.Money, r => r.Profit, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal>("closingQuantity", ColumnDataType.Quantity, r => r.ClosingQuantity, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, decimal>("closingBalance", ColumnDataType.Money, r => r.ClosingBalance, AggregateFunction.Sum),
        ReportColumn.Of<ItemSummaryRow, string?>("inventoryAccount", ColumnDataType.Text, r => r.InventoryAccount),
        ReportColumn.Of<ItemSummaryRow, string?>("purchaseAccount", ColumnDataType.Text, r => r.PurchaseAccount),
        ReportColumn.Of<ItemSummaryRow, string?>("salesAccount", ColumnDataType.Text, r => r.SalesAccount),
        ReportColumn.Of<ItemSummaryRow, long>("itemId", ColumnDataType.Number, r => r.ItemId, filterable: false),
    ];

    /// <summary>SubAccountReferenceType.Item, in Accounting's enum this project does not reference.</summary>
    private const int ItemReference = 2;

    protected override IQueryable<ItemSummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        return from i in db.Items
               join c in db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
               from c in categories.DefaultIfEmpty()
               join u in db.UnitsOfMeasure on i.InventoryUomId equals u.UomId into uoms
               from u in uoms.DefaultIfEmpty()
               let openingMoves = db.StockMovements.Where(m => m.ItemId == i.ItemId && (start == null || m.MovementDate < start))
               let periodMoves = db.StockMovements.Where(m => m.ItemId == i.ItemId && (start == null || m.MovementDate >= start) && (end == null || m.MovementDate <= end))
               
               // Compute opening
               let openingQty = openingMoves.Sum(m => m.Direction == 1 ? m.Quantity : -m.Quantity)
               let openingBal = openingMoves.Sum(m => m.Direction == 1 ? (m.TotalCost ?? 0m) : -(m.TotalCost ?? 0m))

               // Compute period adjustments (Adjustment = 3)
               let adjMoves = periodMoves.Where(m => m.MovementType == 3)
               let adjQty = adjMoves.Sum(m => m.Direction == 1 ? m.Quantity : -m.Quantity)
               let adjBal = adjMoves.Sum(m => m.Direction == 1 ? (m.TotalCost ?? 0m) : -(m.TotalCost ?? 0m))
               
               // Compute period totals for closing
               let periodQty = periodMoves.Sum(m => m.Direction == 1 ? m.Quantity : -m.Quantity)
               let periodBal = periodMoves.Sum(m => m.Direction == 1 ? (m.TotalCost ?? 0m) : -(m.TotalCost ?? 0m))
               
               select new ItemSummaryRow
               {
                   ItemId = i.ItemId,
                   ItemCode = i.ItemCode,
                   ItemName = i.ItemName,
                   ItemGroup = c != null ? c.CategoryName : null,
                   ProductCategory = c != null ? c.CategoryName : null,
                   InventoryType = i.ItemType.ToString(),
                   CostingMethod = i.CostingType.ToString(),
                   UnitOfMeasurement = u != null ? u.UomName : "Unknown",
                   OpeningQuantity = openingQty,
                   OpeningBalance = openingBal,
                   QuantityPurchased = null,
                   Purchases = null,
                   QuantitySold = null,
                   Sales = null,
                   QuantityAdjusted = adjQty,
                   Adjustments = adjBal,
                   Cogs = null,
                   Profit = null,
                   ClosingQuantity = openingQty + periodQty,
                   ClosingBalance = openingBal + periodBal,
                   InventoryAccount = db.SubAccounts
                       .Where(sa => sa.ReferenceType == ItemReference && sa.ReferenceId == i.ItemId)
                       .Join(db.Accounts, sa => sa.AccountId, a => a.AccountId, (sa, a) => a)
                       .Where(a => a.AccountSystemName == "Inventory")
                       .Select(a => a.AccountName)
                       .FirstOrDefault(),
                   // Cost of Goods Sold — see ItemListSource for why "Purchase
                   // Account" resolves to this one.
                   PurchaseAccount = db.SubAccounts
                       .Where(sa => sa.ReferenceType == ItemReference && sa.ReferenceId == i.ItemId)
                       .Join(db.Accounts, sa => sa.AccountId, a => a.AccountId, (sa, a) => a)
                       .Where(a => a.AccountSystemName == "Cost of Goods Sold")
                       .Select(a => a.AccountName)
                       .FirstOrDefault(),
                   SalesAccount = db.SubAccounts
                       .Where(sa => sa.ReferenceType == ItemReference && sa.ReferenceId == i.ItemId)
                       .Join(db.Accounts, sa => sa.AccountId, a => a.AccountId, (sa, a) => a)
                       .Where(a => a.AccountSystemName == "Sales Revenue")
                       .Select(a => a.AccountName)
                       .FirstOrDefault(),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ItemSummaryRow, long>>)(r => r.ItemId);
}

public sealed class ItemSummaryRow
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? ItemGroup { get; set; }
    public string? ProductCategory { get; set; }
    public string InventoryType { get; set; } = null!;
    public string CostingMethod { get; set; } = null!;
    public string UnitOfMeasurement { get; set; } = null!;
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal? QuantityPurchased { get; set; }
    public decimal? Purchases { get; set; }
    public decimal? QuantitySold { get; set; }
    public decimal? Sales { get; set; }
    public decimal QuantityAdjusted { get; set; }
    public decimal Adjustments { get; set; }
    public decimal? Cogs { get; set; }
    public decimal? Profit { get; set; }
    public decimal ClosingQuantity { get; set; }
    public decimal ClosingBalance { get; set; }
    public string? InventoryAccount { get; set; }
    public string? PurchaseAccount { get; set; }
    public string? SalesAccount { get; set; }
}

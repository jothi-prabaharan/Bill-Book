using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Inventory Item Summary for export, saved views and scheduled emails (TK-243). One row for each
/// stocked item that had stock at the From date or moved in the period, from the builders the page uses
/// (<see cref="InventoryItemSummaryReportRepository.Rows"/>): its opening, purchased, sold, adjusted and
/// closing quantities and values, its sales, cost of sales and profit (blank while what was sold has a
/// movement not yet costed), and the names of the accounts its sub-accounts are under. <b>Both dates are
/// required</b> (D-67).
/// </summary>
public sealed class ItemSummarySourceRepository : ReportSource<ItemSummaryRow>
{
    public override string ReportKey => "inventory-item-summary";

    public override string Title => "Inventory Item Summary";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
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
    private const SubAccountReferenceType ItemReference = SubAccountReferenceType.Item;

    protected override IQueryable<ItemSummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new InventoryItemSummaryReportRequest { FromDate = start, ToDate = end };
        IQueryable<ItemRead> items = InventoryItemSummaryReportRepository.Items(db.Items, request);
        IQueryable<InventoryItemSummaryEntry> rows = InventoryItemSummaryReportRepository.Rows(
            items,
            db.ItemCategories,
            db.UnitsOfMeasure,
            InventoryItemSummaryReportRepository.ItemFigures(InventoryItemSummaryReportRepository.MovementFigures(items, db.StockMovements, start, end)),
            InventoryItemSummaryReportRepository.ItemSales(items, db.Invoices, db.InvoiceDetails, db.CreditNotes, db.CreditNoteDetails, start, end),
            showAll: false);

        return from r in rows
               join ia in AccountNames(db, "Inventory") on r.ItemId equals ia.ItemId into inventory
               from ia in inventory.DefaultIfEmpty()
               // Cost of Goods Sold: see ItemListSourceRepository for why "Purchase Account" resolves to this one.
               join pa in AccountNames(db, "Cost of Goods Sold") on r.ItemId equals pa.ItemId into purchase
               from pa in purchase.DefaultIfEmpty()
               join sa in AccountNames(db, "Sales Revenue") on r.ItemId equals sa.ItemId into sold
               from sa in sold.DefaultIfEmpty()
               select new ItemSummaryRow
               {
                   ItemId = r.ItemId,
                   ItemCode = r.ItemCode,
                   ItemName = r.ItemName,
                   ItemGroup = r.CategoryName,
                   ProductCategory = r.CategoryName,
                   InventoryType = r.ItemType.ToString(),
                   CostingMethod = r.CostingType.ToString(),
                   UnitOfMeasurement = r.UnitName ?? "Unknown",
                   OpeningQuantity = r.OpeningQuantity,
                   OpeningBalance = r.OpeningValue,
                   QuantityPurchased = r.PurchasedQuantity,
                   Purchases = r.PurchasedValue,
                   QuantitySold = r.SoldQuantity,
                   Sales = r.Sales,
                   QuantityAdjusted = r.AdjustedQuantity,
                   Adjustments = r.AdjustedValue,
                   Cogs = r.SoldCost,
                   Profit = r.SoldUncosted == 0 ? r.Sales - r.SoldCost : null,
                   ClosingQuantity = r.ClosingQuantity,
                   ClosingBalance = r.ClosingValue,
                   InventoryAccount = ia == null ? null : ia.Name,
                   PurchaseAccount = pa == null ? null : pa.Name,
                   SalesAccount = sa == null ? null : sa.Name,
               };
    }

    /// <summary>The name of the account an item's sub-account under the named system account belongs to: one row for each item that has one. LINQ only.</summary>
    private static IQueryable<ItemAccountName> AccountNames(ReportingDbContext db, string systemName) =>
        from sub in db.SubAccounts
        join a in db.Accounts on sub.AccountId equals a.AccountId
        where sub.ReferenceType == ItemReference && a.AccountSystemName == systemName
        select new ItemAccountName { ItemId = sub.ReferenceId, Name = a.AccountName };

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

/// <summary>The name of an account an item's sub-account is under.</summary>
public sealed class ItemAccountName
{
    public long ItemId { get; set; }

    public string Name { get; set; } = string.Empty;
}

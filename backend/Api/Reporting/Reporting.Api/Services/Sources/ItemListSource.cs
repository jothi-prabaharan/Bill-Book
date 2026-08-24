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
        ReportColumn.Of<ItemListRow, decimal?>("purchaseTaxRate", ColumnDataType.Percent, r => r.PurchaseTaxRate),
        ReportColumn.Of<ItemListRow, decimal?>("salesTaxRate", ColumnDataType.Percent, r => r.SalesTaxRate),
        ReportColumn.Of<ItemListRow, string?>("inventoryAccount", ColumnDataType.Text, r => r.InventoryAccount),
        ReportColumn.Of<ItemListRow, string?>("purchaseAccount", ColumnDataType.Text, r => r.PurchaseAccount),
        ReportColumn.Of<ItemListRow, string?>("salesAccount", ColumnDataType.Text, r => r.SalesAccount),
        ReportColumn.Of<ItemListRow, decimal?>("quantityOnHand", ColumnDataType.Quantity, r => r.QuantityOnHand, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("averageCost", ColumnDataType.Money, r => r.AverageCost),
        ReportColumn.Of<ItemListRow, decimal?>("unitCostPrice", ColumnDataType.Money, r => r.UnitCostPrice),

        // Quantity on hand less what is already committed. **Not the same figure
        // as Quantity On Hand beside it**: a warehouse holding sixty units with
        // fifty reserved can fill an order for ten, and only this column says so.
        ReportColumn.Of<ItemListRow, decimal?>("balanceQty", ColumnDataType.Quantity, r => r.BalanceQty, AggregateFunction.Sum),

        // What the stock still on the shelf actually cost, from the open cost
        // layers rather than from the item's list price. It differs from Average
        // Cost whenever prices have moved, and it is the figure a FIFO item is
        // valued at — an item costed by weighted average has layers too, so this
        // is populated either way and the two columns can be compared.
        ReportColumn.Of<ItemListRow, decimal?>("unitCostPriceFifo", ColumnDataType.Money, r => r.UnitCostPriceFifo),
        ReportColumn.Of<ItemListRow, decimal?>("unitSalePrice", ColumnDataType.Money, r => r.UnitSalePrice),
        ReportColumn.Of<ItemListRow, decimal?>("totalValue", ColumnDataType.Money, r => r.TotalValue, AggregateFunction.Sum),

        // Partial - to be filled later by sal/pur
        ReportColumn.Of<ItemListRow, decimal?>("quantityOnOrder", ColumnDataType.Quantity, r => r.QuantityOnOrder, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("quantityReceived", ColumnDataType.Quantity, r => r.QuantityReceived, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("committedQuotes", ColumnDataType.Quantity, r => r.CommittedQuotes, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("committedToDO", ColumnDataType.Quantity, r => r.CommittedToDO, AggregateFunction.Sum),

        ReportColumn.Of<ItemListRow, long>("itemId", ColumnDataType.Number, r => r.ItemId, filterable: false),
    ];

    /// <summary>SubAccountReferenceType.Item, in Accounting's enum this project does not reference.</summary>
    private const int ItemReference = 2;

    protected override IQueryable<ItemListRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

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
                   Date = i.CreatedAt.HasValue
                       ? DateOnly.FromDateTime(i.CreatedAt!.Value.UtcDateTime)
                       : (DateOnly?)null,
                   UnitOfMeasurement = u != null ? u.UomName : "Unknown",
                   PurchaseDescription = i.Description,
                   SalesDescription = i.Description,
                   // The item's own tax group, effective-dated: the row in force
                   // today, split by whether the item is bought or sold under it.
                   PurchaseTaxRate = i.TaxGroupId == null ? null : db.TaxMasters
                       .Where(tm => tm.TaxGroupId == i.TaxGroupId!.Value && tm.IsPurchase
                           && tm.EffectiveFrom <= today
                           && (tm.EffectiveTo == null || tm.EffectiveTo >= today))
                       .OrderByDescending(tm => tm.EffectiveFrom)
                       .Select(tm => (decimal?)tm.TotalRate)
                       .FirstOrDefault(),
                   SalesTaxRate = i.TaxGroupId == null ? null : db.TaxMasters
                       .Where(tm => tm.TaxGroupId == i.TaxGroupId!.Value && tm.IsSales
                           && tm.EffectiveFrom <= today
                           && (tm.EffectiveTo == null || tm.EffectiveTo >= today))
                       .OrderByDescending(tm => tm.EffectiveFrom)
                       .Select(tm => (decimal?)tm.TotalRate)
                       .FirstOrDefault(),
                   // The item's own three sub-accounts, told apart by which
                   // control account they were provisioned under — see
                   // SubAccountService.ProvisionAsync and AccountRead.AccountSystemName.
                   InventoryAccount = db.SubAccounts
                       .Where(sa => sa.ReferenceType == ItemReference && sa.ReferenceId == i.ItemId)
                       .Join(db.Accounts, sa => sa.AccountId, a => a.AccountId, (sa, a) => a)
                       .Where(a => a.AccountSystemName == "Inventory")
                       .Select(a => a.AccountName)
                       .FirstOrDefault(),
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
                   QuantityOnHand = s != null ? s.QuantityOnHand : 0m,
                   AverageCost = s != null ? s.WeightedAverageCost : 0m,
                   UnitCostPrice = i.PurchasePrice,
                   BalanceQty = s != null ? s.QuantityOnHand - s.QuantityReserved : 0m,
                   // Oldest layer still holding stock. FIFO issues from that one
                   // next, so it is the cost the next unit out will carry.
                   UnitCostPriceFifo = db.CostLayers
                       .Where(cl => cl.ItemId == i.ItemId && cl.RemainingQuantity > 0)
                       .OrderBy(cl => cl.ReceivedOn)
                       .ThenBy(cl => cl.CostLayerId)
                       .Select(cl => (decimal?)cl.UnitCost)
                       .FirstOrDefault(),
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
    public decimal? PurchaseTaxRate { get; set; }
    public decimal? SalesTaxRate { get; set; }
    public string? InventoryAccount { get; set; }

    /// <summary>
    /// The item's Cost of Goods Sold sub-account. There is no separate "Purchase"
    /// system account under perpetual inventory — a purchase debits Inventory
    /// directly (CLAUDE.md: "Purchase: Dr Inventory / Cr Accounts Payable") — so
    /// this is the closest real account to what the column name asks for: where
    /// this item's cost moves to when it is sold.
    /// </summary>
    public string? PurchaseAccount { get; set; }
    public string? SalesAccount { get; set; }
    public decimal? QuantityOnHand { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal? UnitCostPrice { get; set; }

    /// <summary>On hand less reserved — what can actually be promised.</summary>
    public decimal? BalanceQty { get; set; }

    /// <summary>The oldest open cost layer's unit cost.</summary>
    public decimal? UnitCostPriceFifo { get; set; }
    public decimal? UnitSalePrice { get; set; }
    public decimal? TotalValue { get; set; }
    public decimal? QuantityOnOrder { get; set; }
    public decimal? QuantityReceived { get; set; }
    public decimal? CommittedQuotes { get; set; }
    public decimal? CommittedToDO { get; set; }
}

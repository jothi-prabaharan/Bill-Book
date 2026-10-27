using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;

namespace Reporting.Repository.Repositories;

public sealed class ItemListSourceRepository : ReportSource<ItemListRow>
{
    private readonly BatchedNameResolverRepository _resolver;

    public ItemListSourceRepository(BatchedNameResolverRepository resolver)
    {
        _resolver = resolver;
    }

    public override string ReportKey => "inventory-item-list";

    public override string Title => "Inventory Item List";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "asOf", Label = "As at", DataType = ColumnDataType.Date, IsRequired = true },
    ];

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
        ReportColumn.Of<ItemListRow, decimal?>("balanceQty", ColumnDataType.Quantity, r => r.BalanceQty, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("unitCostPriceFifo", ColumnDataType.Money, r => r.UnitCostPriceFifo),
        ReportColumn.Of<ItemListRow, decimal?>("unitSalePrice", ColumnDataType.Money, r => r.UnitSalePrice),
        ReportColumn.Of<ItemListRow, decimal?>("totalValue", ColumnDataType.Money, r => r.TotalValue, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("quantityOnOrder", ColumnDataType.Quantity, r => r.QuantityOnOrder, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("quantityReceived", ColumnDataType.Quantity, r => r.QuantityReceived, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("committedQuotes", ColumnDataType.Quantity, r => r.CommittedQuotes, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, decimal?>("committedToDO", ColumnDataType.Quantity, r => r.CommittedToDO, AggregateFunction.Sum),
        ReportColumn.Of<ItemListRow, long>("itemId", ColumnDataType.Number, r => r.ItemId, filterable: false),
    ];

    private const SubAccountReferenceType ItemReference = SubAccountReferenceType.Item;

    protected override IQueryable<ItemListRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly asOf = parameters.Date("asOf") ?? throw new ReportQueryException("As at date is required.");

        var stockFigures = db.StockMovements
            .Where(m => m.MovementDate <= asOf)
            .GroupBy(m => m.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                QuantityOnHand = g.Sum(m => m.Direction == StockDirection.In ? m.Quantity : -m.Quantity),
                TotalValue = g.Sum(m => m.CostingStatus == CostingStatus.Costed
                    ? (m.Direction == StockDirection.In ? (m.TotalCost ?? 0m) : -(m.TotalCost ?? 0m))
                    : 0m)
            });

        var poFigures = (from po in db.PurchaseOrders
                         join pod in db.PurchaseOrderDetails on po.PurchaseOrderId equals pod.PurchaseOrderId
                         where po.Status == DocumentStatus.Posted && pod.ItemId != null && pod.Quantity > pod.ReceivedQuantity
                         select new { ItemId = pod.ItemId!.Value, Remaining = pod.Quantity - pod.ReceivedQuantity })
                        .GroupBy(x => x.ItemId)
                        .Select(g => new { ItemId = g.Key, QuantityOnOrder = g.Sum(x => x.Remaining) });

        var soFigures = (from so in db.SalesOrders
                         join sod in db.SalesOrderDetails on so.SalesOrderId equals sod.SalesOrderId
                         where so.Status == DocumentStatus.Posted && sod.ItemId != null && sod.Quantity > sod.DeliveredQuantity
                         select new { ItemId = sod.ItemId!.Value, Remaining = sod.Quantity - sod.DeliveredQuantity })
                        .GroupBy(x => x.ItemId)
                        .Select(g => new { ItemId = g.Key, QuantityCommitted = g.Sum(x => x.Remaining) });

        return from i in db.Items
               join s in stockFigures on i.ItemId equals s.ItemId into stocks
               from s in stocks.DefaultIfEmpty()
               join po in poFigures on i.ItemId equals po.ItemId into pos
               from po in pos.DefaultIfEmpty()
               join so in soFigures on i.ItemId equals so.ItemId into sos
               from so in sos.DefaultIfEmpty()
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
                   PurchaseTaxRate = i.TaxGroupId == null ? null : db.TaxMasters
                       .Where(tm => tm.TaxGroupId == i.TaxGroupId!.Value && tm.IsPurchase
                           && tm.EffectiveFrom <= asOf
                           && (tm.EffectiveTo == null || tm.EffectiveTo >= asOf))
                       .OrderByDescending(tm => tm.EffectiveFrom)
                       .Select(tm => (decimal?)tm.TotalRate)
                       .FirstOrDefault(),
                   SalesTaxRate = i.TaxGroupId == null ? null : db.TaxMasters
                       .Where(tm => tm.TaxGroupId == i.TaxGroupId!.Value && tm.IsSales
                           && tm.EffectiveFrom <= asOf
                           && (tm.EffectiveTo == null || tm.EffectiveTo >= asOf))
                       .OrderByDescending(tm => tm.EffectiveFrom)
                       .Select(tm => (decimal?)tm.TotalRate)
                       .FirstOrDefault(),
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
                   AverageCost = (s != null && s.QuantityOnHand != 0m) ? (s.TotalValue / s.QuantityOnHand) : (i.PurchasePrice ?? 0m),
                   UnitCostPrice = i.PurchasePrice,
                   BalanceQty = (s != null ? s.QuantityOnHand : 0m) - (so != null ? so.QuantityCommitted : 0m),
                   UnitCostPriceFifo = db.CostLayers
                       .Where(cl => cl.ItemId == i.ItemId && cl.RemainingQuantity > 0)
                       .OrderBy(cl => cl.ReceivedOn)
                       .ThenBy(cl => cl.CostLayerId)
                       .Select(cl => (decimal?)cl.UnitCost)
                       .FirstOrDefault(),
                   UnitSalePrice = i.SalesPrice,
                   TotalValue = s != null ? s.TotalValue : 0m,
                   QuantityOnOrder = po != null ? (decimal?)po.QuantityOnOrder : 0m,
                   QuantityReceived = 0m,
                   CommittedQuotes = 0m,
                   CommittedToDO = so != null ? (decimal?)so.QuantityCommitted : 0m
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
    public string? PurchaseAccount { get; set; }
    public string? SalesAccount { get; set; }
    public decimal? QuantityOnHand { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal? UnitCostPrice { get; set; }
    public decimal? BalanceQty { get; set; }
    public decimal? UnitCostPriceFifo { get; set; }
    public decimal? UnitSalePrice { get; set; }
    public decimal? TotalValue { get; set; }
    public decimal? QuantityOnOrder { get; set; }
    public decimal? QuantityReceived { get; set; }
    public decimal? CommittedQuotes { get; set; }
    public decimal? CommittedToDO { get; set; }
}

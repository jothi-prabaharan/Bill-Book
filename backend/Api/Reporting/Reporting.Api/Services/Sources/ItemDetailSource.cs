using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class ItemDetailSource : ReportSource<ItemDetailRow>
{
    public override string ReportKey => "inventory-item-detail";

    public override string Title => "Inventory Item Detail";

    public override ReportModule Module => ReportModule.Inventory;

    public override string RequiredPermission => "inventory.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ItemDetailRow, DateOnly>("date", ColumnDataType.Date, r => r.Date),
        ReportColumn.Of<ItemDetailRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<ItemDetailRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<ItemDetailRow, string?>("itemGroup", ColumnDataType.Text, r => r.ItemGroup, groupable: true),
        ReportColumn.Of<ItemDetailRow, string?>("productCategory", ColumnDataType.Text, r => r.ProductCategory, groupable: true),
        ReportColumn.Of<ItemDetailRow, string?>("description", ColumnDataType.Text, r => r.Description),
        ReportColumn.Of<ItemDetailRow, string?>("contactCode", ColumnDataType.Text, r => r.ContactCode),
        ReportColumn.Of<ItemDetailRow, string?>("contactName", ColumnDataType.Text, r => r.ContactName),
        ReportColumn.Of<ItemDetailRow, string>("costingMethod", ColumnDataType.Enum, r => r.CostingMethod, groupable: true),
        ReportColumn.Of<ItemDetailRow, string>("unitOfMeasurement", ColumnDataType.Text, r => r.UnitOfMeasurement, groupable: true),
        ReportColumn.Of<ItemDetailRow, string?>("transactionNo", ColumnDataType.Text, r => r.TransactionNo),
        ReportColumn.Of<ItemDetailRow, string?>("reference", ColumnDataType.Text, r => r.Reference),
        ReportColumn.Of<ItemDetailRow, string>("source", ColumnDataType.Enum, r => r.Source, groupable: true),
        ReportColumn.Of<ItemDetailRow, decimal>("qohMovement", ColumnDataType.Quantity, r => r.QoHMovement, AggregateFunction.Sum),
        ReportColumn.Of<ItemDetailRow, decimal>("valueMovement", ColumnDataType.Money, r => r.ValueMovement, AggregateFunction.Sum),
        ReportColumn.Of<ItemDetailRow, decimal?>("unitCostPrice", ColumnDataType.Money, r => r.UnitCostPrice),
        
        // Partial Columns
        ReportColumn.Of<ItemDetailRow, decimal?>("unitSalePrice", ColumnDataType.Money, r => r.UnitSalePrice),
        ReportColumn.Of<ItemDetailRow, decimal?>("margin", ColumnDataType.Money, r => r.Margin),
        ReportColumn.Of<ItemDetailRow, decimal?>("profitPerItem", ColumnDataType.Money, r => r.ProfitPerItem),
        ReportColumn.Of<ItemDetailRow, string?>("inventoryAccount", ColumnDataType.Text, r => r.InventoryAccount),
        ReportColumn.Of<ItemDetailRow, string?>("purchaseAccount", ColumnDataType.Text, r => r.PurchaseAccount),
        ReportColumn.Of<ItemDetailRow, string?>("salesAccount", ColumnDataType.Text, r => r.SalesAccount),
        ReportColumn.Of<ItemDetailRow, string?>("adjustmentAccount", ColumnDataType.Text, r => r.AdjustmentAccount),

        ReportColumn.Of<ItemDetailRow, long>("stockMovementId", ColumnDataType.Number, r => r.StockMovementId, filterable: false),
    ];

    /// <summary>SubAccountReferenceType.Item, in Accounting's enum this project does not reference.</summary>
    private const int ItemReference = 2;

    protected override IQueryable<ItemDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        IQueryable<Repository.ReadModels.StockMovementRead> movements = db.StockMovements;

        if (start is DateOnly from)
        {
            movements = movements.Where(m => m.MovementDate >= from);
        }

        if (end is DateOnly to)
        {
            movements = movements.Where(m => m.MovementDate <= to);
        }

        return from m in movements
               join i in db.Items on m.ItemId equals i.ItemId
               join c in db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
               from c in categories.DefaultIfEmpty()
               join u in db.UnitsOfMeasure on i.InventoryUomId equals u.UomId into uoms
               from u in uoms.DefaultIfEmpty()
               let sign = m.Direction == 1 ? 1m : -1m // 1 = Inbound, 2 = Outbound
               select new ItemDetailRow
               {
                   StockMovementId = m.StockMovementId,
                   Date = m.MovementDate,
                   ItemCode = i.ItemCode,
                   ItemName = i.ItemName,
                   ItemGroup = c != null ? c.CategoryName : null,
                   ProductCategory = c != null ? c.CategoryName : null,
                   Description = m.Notes ?? i.Description,
                   ContactCode = null,
                   ContactName = null,
                   CostingMethod = i.CostingType.ToString(),
                   UnitOfMeasurement = u != null ? u.UomName : "Unknown",
                   TransactionNo = m.SourceId != null ? m.SourceType + "-" + m.SourceId : null,
                   // Built the same way TransactionNo two lines above is — the
                   // movement carries no separate reference field, and every
                   // other source in this product uses the source document's own
                   // type-and-id as both.
                   Reference = m.SourceId != null ? m.SourceType + "-" + m.SourceId : null,
                   Source = m.MovementType.ToString(),
                   QoHMovement = m.Quantity * sign,
                   ValueMovement = (m.TotalCost ?? 0m) * sign,
                   UnitCostPrice = m.UnitCost,
                   UnitSalePrice = null,
                   Margin = null,
                   ProfitPerItem = null,
                   InventoryAccount = db.SubAccounts
                       .Where(sa => sa.ReferenceType == ItemReference && sa.ReferenceId == i.ItemId)
                       .Join(db.Accounts, sa => sa.AccountId, a => a.AccountId, (sa, a) => a)
                       .Where(a => a.AccountSystemName == "Inventory")
                       .Select(a => a.AccountName)
                       .FirstOrDefault(),
                   // Cost of Goods Sold — see ItemListSource for why "Purchase
                   // Account" resolves to this one; there is no separate system
                   // account for a purchase under perpetual inventory.
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
                   // No per-item Adjustment account exists — a stock adjustment
                   // posts to the item's own Inventory sub-account plus an
                   // org-level adjustment account, not a fourth per-item one.
                   AdjustmentAccount = null
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ItemDetailRow, long>>)(r => r.StockMovementId);
}

public sealed class ItemDetailRow
{
    public long StockMovementId { get; set; }
    public DateOnly Date { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? ItemGroup { get; set; }
    public string? ProductCategory { get; set; }
    public string? Description { get; set; }
    public string? ContactCode { get; set; }
    public string? ContactName { get; set; }
    public string CostingMethod { get; set; } = null!;
    public string UnitOfMeasurement { get; set; } = null!;
    public string? TransactionNo { get; set; }
    public string? Reference { get; set; }
    public string Source { get; set; } = null!;
    public decimal QoHMovement { get; set; }
    public decimal ValueMovement { get; set; }
    public decimal? UnitCostPrice { get; set; }
    public decimal? UnitSalePrice { get; set; }
    public decimal? Margin { get; set; }
    public decimal? ProfitPerItem { get; set; }
    public string? InventoryAccount { get; set; }
    public string? PurchaseAccount { get; set; }
    public string? SalesAccount { get; set; }
    public string? AdjustmentAccount { get; set; }
}

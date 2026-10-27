using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;

namespace Reporting.Repository.Repositories;

public sealed class ItemStockFigure
{
    public long ItemId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal TotalValue { get; set; }
    public int UncostedMoves { get; set; }
}

public sealed class ItemPoFigure
{
    public long ItemId { get; set; }
    public decimal QuantityOnOrder { get; set; }
}

public sealed class ItemSoFigure
{
    public long ItemId { get; set; }
    public decimal QuantityCommitted { get; set; }
}

public sealed class ItemFifoFigure
{
    public long ItemId { get; set; }
    public decimal UnitCostPriceFifo { get; set; }
}

public sealed class ItemRowIntermediate
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public long? ItemCategoryId { get; set; }
    public string? CategoryName { get; set; }
    public ItemType ItemType { get; set; }
    public CostingType CostingType { get; set; }
    public bool IsActive { get; set; }
    public string? UnitCode { get; set; }
    public string? Description { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? SalesPrice { get; set; }
    public long? TaxGroupId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal TotalValue { get; set; }
    public bool CostPending { get; set; }
    public decimal QuantityOnOrder { get; set; }
    public decimal QuantityCommitted { get; set; }
    public decimal? FifoCost { get; set; }
}

public sealed class InventoryItemListReportRepository
{
    private readonly ReportingDbContext _db;

    public InventoryItemListReportRepository(ReportingDbContext db)
    {
        _db = db;
    }

    /// <summary>Filtered items query.</summary>
    public static IQueryable<ItemRead> FilterItems(IQueryable<ItemRead> items, InventoryItemListReportRequest request)
    {
        IQueryable<ItemRead> q = items;

        if (request.ItemCategoryId.HasValue)
        {
            q = q.Where(i => i.ItemCategoryId == request.ItemCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(i => i.IsActive);
            }
            else if (request.Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(i => !i.IsActive);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ItemType) && !request.ItemType.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<ItemType>(request.ItemType, true, out var parsedType))
            {
                q = q.Where(i => i.ItemType == parsedType);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string term = request.Search.Trim().ToLower();
            q = q.Where(i => i.ItemCode.ToLower().Contains(term) || i.ItemName.ToLower().Contains(term));
        }

        return q;
    }

    /// <summary>Stock movement figures up to as-at date.</summary>
    public static IQueryable<ItemStockFigure> StockFigures(IQueryable<StockMovementRead> movements, DateOnly asOf)
    {
        return movements
            .Where(m => m.MovementDate <= asOf)
            .GroupBy(m => m.ItemId)
            .Select(g => new ItemStockFigure
            {
                ItemId = g.Key,
                QuantityOnHand = g.Sum(m => m.Direction == StockDirection.In ? m.Quantity : -m.Quantity),
                TotalValue = g.Sum(m => m.CostingStatus == CostingStatus.Costed
                    ? (m.Direction == StockDirection.In ? (m.TotalCost ?? 0m) : -(m.TotalCost ?? 0m))
                    : 0m),
                UncostedMoves = g.Sum(m => (m.CostingStatus != CostingStatus.Costed && m.CostingStatus != CostingStatus.Skipped) ? 1 : 0)
            });
    }

    /// <summary>Open Purchase Order quantities (On Order).</summary>
    public static IQueryable<ItemPoFigure> PoFigures(IQueryable<PurchaseOrderRead> pos, IQueryable<PurchaseOrderDetailRead> pods)
    {
        return (from po in pos
                join pod in pods on po.PurchaseOrderId equals pod.PurchaseOrderId
                where po.Status == DocumentStatus.Posted
                   && pod.ItemId != null
                   && pod.Quantity > pod.ReceivedQuantity
                select new { ItemId = pod.ItemId!.Value, Remaining = pod.Quantity - pod.ReceivedQuantity })
               .GroupBy(x => x.ItemId)
               .Select(g => new ItemPoFigure
               {
                   ItemId = g.Key,
                   QuantityOnOrder = g.Sum(x => x.Remaining)
               });
    }

    /// <summary>Open Sales Order quantities (Committed).</summary>
    public static IQueryable<ItemSoFigure> SoFigures(IQueryable<SalesOrderRead> sos, IQueryable<SalesOrderDetailRead> sods)
    {
        return (from so in sos
                join sod in sods on so.SalesOrderId equals sod.SalesOrderId
                where so.Status == DocumentStatus.Posted
                   && sod.ItemId != null
                   && sod.Quantity > sod.DeliveredQuantity
                select new { ItemId = sod.ItemId!.Value, Remaining = sod.Quantity - sod.DeliveredQuantity })
               .GroupBy(x => x.ItemId)
               .Select(g => new ItemSoFigure
               {
                   ItemId = g.Key,
                   QuantityCommitted = g.Sum(x => x.Remaining)
               });
    }

    /// <summary>Oldest open cost layer for FIFO unit cost.</summary>
    public static IQueryable<ItemFifoFigure> FifoFigures(IQueryable<CostLayerRead> layers)
    {
        return layers
            .Where(cl => cl.RemainingQuantity > 0)
            .GroupBy(cl => cl.ItemId)
            .Select(g => new ItemFifoFigure
            {
                ItemId = g.Key,
                UnitCostPriceFifo = g.OrderBy(cl => cl.ReceivedOn).ThenBy(cl => cl.CostLayerId).Select(cl => cl.UnitCost).FirstOrDefault()
            });
    }

    public async Task<InventoryItemListReportDto> GetReportAsync(InventoryItemListReportRequest request, CancellationToken cancellationToken = default)
    {
        DateOnly asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        IQueryable<ItemRead> filteredItems = FilterItems(_db.Items.AsNoTracking(), request);
        IQueryable<ItemStockFigure> stockQuery = StockFigures(_db.StockMovements.AsNoTracking(), asOf);
        IQueryable<ItemPoFigure> poQuery = PoFigures(_db.PurchaseOrders.AsNoTracking(), _db.PurchaseOrderDetails.AsNoTracking());
        IQueryable<ItemSoFigure> soQuery = SoFigures(_db.SalesOrders.AsNoTracking(), _db.SalesOrderDetails.AsNoTracking());
        IQueryable<ItemFifoFigure> fifoQuery = FifoFigures(_db.CostLayers.AsNoTracking());

        var intermediateQuery = from i in filteredItems
                                join c in _db.ItemCategories.AsNoTracking() on i.ItemCategoryId equals c.ItemCategoryId into cats
                                from c in cats.DefaultIfEmpty()
                                join u in _db.UnitsOfMeasure.AsNoTracking() on i.InventoryUomId equals u.UomId into uoms
                                from u in uoms.DefaultIfEmpty()
                                join st in stockQuery on i.ItemId equals st.ItemId into stocks
                                from st in stocks.DefaultIfEmpty()
                                join po in poQuery on i.ItemId equals po.ItemId into pos
                                from po in pos.DefaultIfEmpty()
                                join so in soQuery on i.ItemId equals so.ItemId into sos
                                from so in sos.DefaultIfEmpty()
                                join fifo in fifoQuery on i.ItemId equals fifo.ItemId into fifos
                                from fifo in fifos.DefaultIfEmpty()
                                select new ItemRowIntermediate
                                {
                                    ItemId = i.ItemId,
                                    ItemCode = i.ItemCode,
                                    ItemName = i.ItemName,
                                    ItemCategoryId = i.ItemCategoryId,
                                    CategoryName = c != null ? c.CategoryName : null,
                                    ItemType = i.ItemType,
                                    CostingType = i.CostingType,
                                    IsActive = i.IsActive,
                                    UnitCode = u != null ? u.UomCode : null,
                                    Description = i.Description,
                                    PurchasePrice = i.PurchasePrice,
                                    SalesPrice = i.SalesPrice,
                                    TaxGroupId = i.TaxGroupId,
                                    QuantityOnHand = st != null ? st.QuantityOnHand : 0m,
                                    TotalValue = st != null ? st.TotalValue : 0m,
                                    CostPending = st != null && st.UncostedMoves > 0,
                                    QuantityOnOrder = po != null ? po.QuantityOnOrder : 0m,
                                    QuantityCommitted = so != null ? so.QuantityCommitted : 0m,
                                    FifoCost = fifo != null ? fifo.UnitCostPriceFifo : (decimal?)null,
                                };

        if (request.OnlyWithStock)
        {
            intermediateQuery = intermediateQuery.Where(x => x.QuantityOnHand != 0m);
        }

        int totalItems = await intermediateQuery.CountAsync(cancellationToken);

        var totals = await intermediateQuery.GroupBy(_ => 1).Select(g => new InventoryItemListTotalsDto
        {
            TotalQuantityOnHand = g.Sum(x => x.QuantityOnHand),
            TotalValue = g.Sum(x => x.TotalValue),
            TotalQuantityOnOrder = g.Sum(x => x.QuantityOnOrder),
            TotalQuantityCommitted = g.Sum(x => x.QuantityCommitted),
            TotalBalanceQty = g.Sum(x => x.QuantityOnHand - x.QuantityCommitted),
            CostPending = g.Any(x => x.CostPending)
        }).FirstOrDefaultAsync(cancellationToken) ?? new InventoryItemListTotalsDto();

        var pageItems = await intermediateQuery
            .OrderBy(x => x.ItemCode)
            .ThenBy(x => x.ItemId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var itemIds = pageItems.Select(x => x.ItemId).ToList();

        // Batched lookups for subaccounts (inventory, purchase, sales)
        var subAccounts = await (from sa in _db.SubAccounts.AsNoTracking()
                                 join a in _db.Accounts.AsNoTracking() on sa.AccountId equals a.AccountId
                                 where sa.ReferenceType == SubAccountReferenceType.Item && itemIds.Contains(sa.ReferenceId)
                                 select new { sa.ReferenceId, a.AccountSystemName, a.AccountName })
                                .ToListAsync(cancellationToken);

        // Batched tax rates
        var taxGroupIds = pageItems.Where(x => x.TaxGroupId.HasValue).Select(x => x.TaxGroupId!.Value).Distinct().ToList();
        var taxMasters = await _db.TaxMasters.AsNoTracking()
            .Where(tm => taxGroupIds.Contains(tm.TaxGroupId)
                && tm.EffectiveFrom <= asOf
                && (tm.EffectiveTo == null || tm.EffectiveTo >= asOf))
            .OrderByDescending(tm => tm.EffectiveFrom)
            .ToListAsync(cancellationToken);

        var itemsList = new List<InventoryItemListItemDto>(pageItems.Count);
        foreach (var p in pageItems)
        {
            decimal avgCost = p.QuantityOnHand != 0m
                ? Math.Round(p.TotalValue / p.QuantityOnHand, 4, MidpointRounding.AwayFromZero)
                : (p.PurchasePrice ?? 0m);

            decimal fifoCost = p.FifoCost ?? (p.PurchasePrice ?? avgCost);

            string? invAccount = subAccounts
                .Where(sa => sa.ReferenceId == p.ItemId && sa.AccountSystemName == "Inventory")
                .Select(sa => sa.AccountName)
                .FirstOrDefault();

            string? purAccount = subAccounts
                .Where(sa => sa.ReferenceId == p.ItemId && sa.AccountSystemName == "Cost of Goods Sold")
                .Select(sa => sa.AccountName)
                .FirstOrDefault();

            string? salAccount = subAccounts
                .Where(sa => sa.ReferenceId == p.ItemId && sa.AccountSystemName == "Sales Revenue")
                .Select(sa => sa.AccountName)
                .FirstOrDefault();

            decimal? purTax = p.TaxGroupId.HasValue
                ? taxMasters.Where(tm => tm.TaxGroupId == p.TaxGroupId.Value && tm.IsPurchase).Select(tm => (decimal?)tm.TotalRate).FirstOrDefault()
                : null;

            decimal? salTax = p.TaxGroupId.HasValue
                ? taxMasters.Where(tm => tm.TaxGroupId == p.TaxGroupId.Value && tm.IsSales).Select(tm => (decimal?)tm.TotalRate).FirstOrDefault()
                : null;

            itemsList.Add(new InventoryItemListItemDto
            {
                ItemId = p.ItemId,
                ItemCode = p.ItemCode,
                ItemName = p.ItemName,
                ItemCategoryId = p.ItemCategoryId,
                CategoryName = p.CategoryName,
                ItemType = p.ItemType.ToString(),
                CostingType = p.CostingType.ToString(),
                Status = p.IsActive ? "Active" : "Inactive",
                UnitCode = p.UnitCode,
                Description = p.Description,
                PurchasePrice = p.PurchasePrice,
                SalesPrice = p.SalesPrice,
                PurchaseTaxRate = purTax,
                SalesTaxRate = salTax,
                InventoryAccount = invAccount,
                PurchaseAccount = purAccount,
                SalesAccount = salAccount,
                QuantityOnHand = p.QuantityOnHand,
                AverageCost = avgCost,
                UnitCostPriceFifo = fifoCost,
                TotalValue = p.TotalValue,
                QuantityOnOrder = p.QuantityOnOrder,
                QuantityCommitted = p.QuantityCommitted,
                BalanceQty = p.QuantityOnHand - p.QuantityCommitted,
                CostPending = p.CostPending
            });
        }

        return new InventoryItemListReportDto
        {
            AsOfDate = asOf,
            BaseCurrencyCode = "INR",
            Items = itemsList,
            TotalItems = totalItems,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Totals = totals
        };
    }

    /// <summary>Categories lookup for filter dropdown.</summary>
    public async Task<IReadOnlyList<InventoryItemListCategoryChoice>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.ItemCategories.AsNoTracking()
            .OrderBy(c => c.CategoryName)
            .Select(c => new InventoryItemListCategoryChoice
            {
                ItemCategoryId = c.ItemCategoryId,
                CategoryName = c.CategoryName
            })
            .ToListAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Inventory Aging Report (TK-246): ages inventory from cost layer receipt dates up to as-at date.
/// Buckets: 0-30, 31-60, 61-90, 91-180, 180+ days.
/// FIFO-correct: remaining quantities of inbound cost layers reflect the true age of current inventory.
/// </summary>
public sealed class InventoryAgingReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public InventoryAgingReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond the date being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(InventoryAgingReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (!request.AsOfDate.HasValue)
        {
            refusals.Add((nameof(InventoryAgingReportRequest.AsOfDate), "As at date is required."));
        }

        return refusals;
    }

    /// <summary>Checks category or warehouse scope if specified.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> ValidateScopeAsync(
        InventoryAgingReportRequest request,
        CancellationToken cancellationToken = default)
    {
        List<(string, string)> refusals = [];

        if (request.ItemCategoryId.HasValue)
        {
            bool exists = await _db.ItemCategories.AsNoTracking()
                .AnyAsync(c => c.ItemCategoryId == request.ItemCategoryId.Value, cancellationToken);
            if (!exists)
            {
                refusals.Add((nameof(InventoryAgingReportRequest.ItemCategoryId), "Selected item category does not exist."));
            }
        }

        if (request.WarehouseId.HasValue)
        {
            bool exists = await _db.Warehouses.AsNoTracking()
                .AnyAsync(w => w.WarehouseId == request.WarehouseId.Value, cancellationToken);
            if (!exists)
            {
                refusals.Add((nameof(InventoryAgingReportRequest.WarehouseId), "Selected warehouse does not exist."));
            }
        }

        return refusals;
    }

    /// <summary>Fetches the Inventory Aging Report.</summary>
    public async Task<InventoryAgingReportDto> GetReportAsync(
        InventoryAgingReportRequest request,
        CancellationToken cancellationToken = default)
    {
        string currencyCode = await _currency.GetBaseCurrencyAsync(cancellationToken) ?? "INR";
        DateOnly asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        int asOfDay = asOf.DayNumber;

        IQueryable<CostLayerRead> layersQuery = _db.CostLayers.AsNoTracking()
            .Where(l => l.RemainingQuantity > 0m && l.ReceivedOn <= asOf);

        if (request.WarehouseId.HasValue)
        {
            IQueryable<StockMovementRead> movements = _db.StockMovements.AsNoTracking()
                .Where(sm => sm.WarehouseId == request.WarehouseId.Value);

            layersQuery = layersQuery.Join(
                movements,
                l => l.StockMovementId,
                sm => sm.StockMovementId,
                (l, sm) => l);
        }

        var layerFigures = layersQuery
            .GroupBy(l => l.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                TotalQuantity = g.Sum(l => l.RemainingQuantity),
                TotalValue = g.Sum(l => l.RemainingQuantity * l.UnitCost),
                Qty0To30 = g.Sum(l => asOfDay - l.ReceivedOn.DayNumber <= 30 ? l.RemainingQuantity : 0m),
                Val0To30 = g.Sum(l => asOfDay - l.ReceivedOn.DayNumber <= 30 ? l.RemainingQuantity * l.UnitCost : 0m),
                Qty31To60 = g.Sum(l => asOfDay - l.ReceivedOn.DayNumber > 30 && asOfDay - l.ReceivedOn.DayNumber <= 60 ? l.RemainingQuantity : 0m),
                Val31To60 = g.Sum(l => asOfDay - l.ReceivedOn.DayNumber > 30 && asOfDay - l.ReceivedOn.DayNumber <= 60 ? l.RemainingQuantity * l.UnitCost : 0m),
                Qty61To90 = g.Sum(l => asOfDay - l.ReceivedOn.DayNumber > 60 && asOfDay - l.ReceivedOn.DayNumber <= 90 ? l.RemainingQuantity : 0m),
                Val61To90 = g.Sum(l => asOfDay - l.ReceivedOn.DayNumber > 60 && asOfDay - l.ReceivedOn.DayNumber <= 90 ? l.RemainingQuantity * l.UnitCost : 0m),
                Qty91To180 = g.Sum(l => asOfDay - l.ReceivedOn.DayNumber > 90 && asOfDay - l.ReceivedOn.DayNumber <= 180 ? l.RemainingQuantity : 0m),
                Val91To180 = g.Sum(l => asOfDay - l.ReceivedOn.DayNumber > 90 && asOfDay - l.ReceivedOn.DayNumber <= 180 ? l.RemainingQuantity * l.UnitCost : 0m),
                QtyOver180 = g.Sum(l => asOfDay - l.ReceivedOn.DayNumber > 180 ? l.RemainingQuantity : 0m),
                ValOver180 = g.Sum(l => asOfDay - l.ReceivedOn.DayNumber > 180 ? l.RemainingQuantity * l.UnitCost : 0m)
            });

        IQueryable<ItemRead> itemsQuery = _db.Items.AsNoTracking();

        if (request.ItemCategoryId.HasValue)
        {
            itemsQuery = itemsQuery.Where(i => i.ItemCategoryId == request.ItemCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string term = request.Search.Trim().ToLower();
            itemsQuery = itemsQuery.Where(i => i.ItemCode.ToLower().Contains(term) || i.ItemName.ToLower().Contains(term));
        }

        var query = from i in itemsQuery
                    join c in _db.ItemCategories.AsNoTracking() on i.ItemCategoryId equals c.ItemCategoryId into cats
                    from c in cats.DefaultIfEmpty()
                    join u in _db.UnitsOfMeasure.AsNoTracking() on i.InventoryUomId equals u.UomId into uoms
                    from u in uoms.DefaultIfEmpty()
                    join f in layerFigures on i.ItemId equals f.ItemId into figs
                    from f in figs.DefaultIfEmpty()
                    where !request.OnlyWithStock || (f != null && f.TotalQuantity > 0m)
                    select new
                    {
                        i.ItemId,
                        i.ItemCode,
                        i.ItemName,
                        i.ItemCategoryId,
                        CategoryName = c != null ? c.CategoryName : null,
                        UnitCode = u != null ? u.UomCode : null,
                        CostingType = i.CostingType.ToString(),
                        TotalQuantity = f != null ? f.TotalQuantity : 0m,
                        TotalValue = f != null ? f.TotalValue : 0m,
                        Qty0To30 = f != null ? f.Qty0To30 : 0m,
                        Val0To30 = f != null ? f.Val0To30 : 0m,
                        Qty31To60 = f != null ? f.Qty31To60 : 0m,
                        Val31To60 = f != null ? f.Val31To60 : 0m,
                        Qty61To90 = f != null ? f.Qty61To90 : 0m,
                        Val61To90 = f != null ? f.Val61To90 : 0m,
                        Qty91To180 = f != null ? f.Qty91To180 : 0m,
                        Val91To180 = f != null ? f.Val91To180 : 0m,
                        QtyOver180 = f != null ? f.QtyOver180 : 0m,
                        ValOver180 = f != null ? f.ValOver180 : 0m
                    };

        int totalItems = await query.CountAsync(cancellationToken);

        var totalsAgg = totalItems > 0
            ? await query.GroupBy(_ => 1).Select(g => new
            {
                TotalQuantity = g.Sum(x => x.TotalQuantity),
                TotalValue = g.Sum(x => x.TotalValue),
                Qty0To30 = g.Sum(x => x.Qty0To30),
                Val0To30 = g.Sum(x => x.Val0To30),
                Qty31To60 = g.Sum(x => x.Qty31To60),
                Val31To60 = g.Sum(x => x.Val31To60),
                Qty61To90 = g.Sum(x => x.Qty61To90),
                Val61To90 = g.Sum(x => x.Val61To90),
                Qty91To180 = g.Sum(x => x.Qty91To180),
                Val91To180 = g.Sum(x => x.Val91To180),
                QtyOver180 = g.Sum(x => x.QtyOver180),
                ValOver180 = g.Sum(x => x.ValOver180),
            }).FirstOrDefaultAsync(cancellationToken)
            : null;

        var totals = new InventoryAgingTotalsDto
        {
            TotalQuantity = totalsAgg?.TotalQuantity ?? 0m,
            TotalValue = totalsAgg?.TotalValue ?? 0m,
            Bucket0To30 = new InventoryAgingBucketDto { Quantity = totalsAgg?.Qty0To30 ?? 0m, Value = totalsAgg?.Val0To30 ?? 0m },
            Bucket31To60 = new InventoryAgingBucketDto { Quantity = totalsAgg?.Qty31To60 ?? 0m, Value = totalsAgg?.Val31To60 ?? 0m },
            Bucket61To90 = new InventoryAgingBucketDto { Quantity = totalsAgg?.Qty61To90 ?? 0m, Value = totalsAgg?.Val61To90 ?? 0m },
            Bucket91To180 = new InventoryAgingBucketDto { Quantity = totalsAgg?.Qty91To180 ?? 0m, Value = totalsAgg?.Val91To180 ?? 0m },
            BucketOver180 = new InventoryAgingBucketDto { Quantity = totalsAgg?.QtyOver180 ?? 0m, Value = totalsAgg?.ValOver180 ?? 0m }
        };

        int pageNumber = Math.Max(1, request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 200);

        var pageRows = await query
            .OrderBy(x => x.ItemCode)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pageRows.Select(p => new InventoryAgingItemDto
        {
            ItemId = p.ItemId,
            ItemCode = p.ItemCode,
            ItemName = p.ItemName,
            ItemCategoryId = p.ItemCategoryId,
            CategoryName = p.CategoryName,
            UnitCode = p.UnitCode,
            CostingType = p.CostingType,
            TotalQuantity = p.TotalQuantity,
            TotalValue = p.TotalValue,
            Bucket0To30 = new InventoryAgingBucketDto { Quantity = p.Qty0To30, Value = p.Val0To30 },
            Bucket31To60 = new InventoryAgingBucketDto { Quantity = p.Qty31To60, Value = p.Val31To60 },
            Bucket61To90 = new InventoryAgingBucketDto { Quantity = p.Qty61To90, Value = p.Val61To90 },
            Bucket91To180 = new InventoryAgingBucketDto { Quantity = p.Qty91To180, Value = p.Val91To180 },
            BucketOver180 = new InventoryAgingBucketDto { Quantity = p.QtyOver180, Value = p.ValOver180 }
        }).ToList();

        return new InventoryAgingReportDto
        {
            AsOfDate = asOf,
            BaseCurrencyCode = currencyCode,
            Items = items,
            TotalItems = totalItems,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Totals = totals
        };
    }

    /// <summary>Categories for the category filter dropdown.</summary>
    public async Task<IReadOnlyList<InventoryAgingCategoryChoice>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.ItemCategories.AsNoTracking()
            .OrderBy(c => c.CategoryName)
            .Select(c => new InventoryAgingCategoryChoice
            {
                ItemCategoryId = c.ItemCategoryId,
                CategoryName = c.CategoryName
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>Warehouses for the warehouse filter dropdown.</summary>
    public async Task<IReadOnlyList<InventoryAgingWarehouseChoice>> GetWarehousesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Warehouses.AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.WarehouseName)
            .Select(w => new InventoryAgingWarehouseChoice
            {
                WarehouseId = w.WarehouseId,
                WarehouseCode = w.WarehouseCode,
                WarehouseName = w.WarehouseName
            })
            .ToListAsync(cancellationToken);
    }
}

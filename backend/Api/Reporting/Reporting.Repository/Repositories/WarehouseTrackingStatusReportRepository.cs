using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Warehouse Tracking Status Report (TK-251): dedicated endpoint and page for each warehouse
/// as at a date: quantities received, issued, and available, item by item, with branch average valuation.
/// </summary>
public sealed class WarehouseTrackingStatusReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;
    private readonly BatchedNameResolverRepository _resolver;

    public WarehouseTrackingStatusReportRepository(
        ReportingDbContext db,
        IBaseCurrencyProvider currency,
        BatchedNameResolverRepository resolver)
    {
        _db = db;
        _currency = currency;
        _resolver = resolver;
    }

    /// <summary>Validation refusals for required fields.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(WarehouseTrackingStatusReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.AsOfDate == null)
        {
            refusals.Add((nameof(WarehouseTrackingStatusReportRequest.AsOfDate), "As at date is required."));
        }

        return refusals;
    }

    /// <summary>Validation for warehouse or category not on this branch.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> LookupRefusalsAsync(WarehouseTrackingStatusReportRequest request, CancellationToken ct)
    {
        List<(string, string)> refusals = [];

        if (request.WarehouseId is long wid && !await _db.Warehouses.AnyAsync(w => w.WarehouseId == wid, ct))
        {
            refusals.Add((nameof(WarehouseTrackingStatusReportRequest.WarehouseId), "That warehouse is not on this branch."));
        }

        if (request.ItemCategoryId is long cid && !await _db.ItemCategories.AnyAsync(c => c.ItemCategoryId == cid, ct))
        {
            refusals.Add((nameof(WarehouseTrackingStatusReportRequest.ItemCategoryId), "That item category is not on this branch."));
        }

        return refusals;
    }

    /// <summary>Warehouses on this branch for the filter dropdown.</summary>
    public Task<List<SalesAnalysisWarehouseChoiceDto>> WarehousesAsync(CancellationToken ct) =>
        SalesAnalysisReportRepository.WarehouseChoices(_db.Warehouses).ToListAsync(ct);

    /// <summary>Item categories on this branch for the filter dropdown.</summary>
    public async Task<List<WarehouseTrackingCategoryChoiceDto>> CategoriesAsync(CancellationToken ct)
    {
        return await _db.ItemCategories
            .OrderBy(c => c.CategoryName)
            .Select(c => new WarehouseTrackingCategoryChoiceDto
            {
                ItemCategoryId = c.ItemCategoryId,
                CategoryName = c.CategoryName
            })
            .ToListAsync(ct);
    }

    public async Task<WarehouseTrackingStatusReportResultDto> RunAsync(WarehouseTrackingStatusReportRequest request, CancellationToken ct)
    {
        DateOnly asOf = request.AsOfDate!.Value;
        string currency = await _currency.GetBaseCurrencyAsync(ct) ?? "INR";

        Dictionary<int, string> states = await _resolver.GetStateNamesAsync(ct);
        Dictionary<int, string> countries = await _resolver.GetCountryNamesAsync(ct);

        // 1. Calculate branch-level average cost per item across all warehouses
        var branchStockMovements = await _db.StockMovements
            .Where(m => m.MovementDate <= asOf)
            .GroupBy(m => m.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                QuantityOnHand = g.Sum(m => m.Direction == StockDirection.In ? m.Quantity : -m.Quantity),
                TotalCostValue = g.Sum(m => m.CostingStatus == CostingStatus.Costed
                    ? (m.Direction == StockDirection.In ? (m.TotalCost ?? 0m) : -(m.TotalCost ?? 0m))
                    : 0m)
            })
            .ToListAsync(ct);

        var itemPrices = await _db.Items
            .Select(i => new { i.ItemId, i.PurchasePrice })
            .ToDictionaryAsync(i => i.ItemId, i => i.PurchasePrice ?? 0m, ct);

        Dictionary<long, decimal> branchAverageCosts = [];
        foreach (var stock in branchStockMovements)
        {
            decimal avgCost = stock.QuantityOnHand != 0m
                ? Math.Round(stock.TotalCostValue / stock.QuantityOnHand, 4, MidpointRounding.AwayFromZero)
                : (itemPrices.TryGetValue(stock.ItemId, out decimal p) ? p : 0m);

            if (avgCost < 0m) avgCost = itemPrices.TryGetValue(stock.ItemId, out decimal defP) ? defP : 0m;
            branchAverageCosts[stock.ItemId] = avgCost;
        }

        // 2. Query warehouses
        var warehousesQuery = _db.Warehouses.AsQueryable();
        if (request.WarehouseId is long reqWid)
        {
            warehousesQuery = warehousesQuery.Where(w => w.WarehouseId == reqWid);
        }

        var warehouses = await warehousesQuery
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.WarehouseName)
            .ToListAsync(ct);

        // 3. Query movements per warehouse up to asOf
        var movementsQuery = from m in _db.StockMovements
                             where m.MovementDate <= asOf && m.WarehouseId != null
                             select m;

        if (request.WarehouseId is long wid)
        {
            movementsQuery = movementsQuery.Where(m => m.WarehouseId == wid);
        }

        var movementsSummary = await movementsQuery
            .GroupBy(m => new { WarehouseId = m.WarehouseId!.Value, m.ItemId })
            .Select(g => new
            {
                g.Key.WarehouseId,
                g.Key.ItemId,
                QuantityIn = g.Sum(m => m.Direction == StockDirection.In ? m.Quantity : 0m),
                QuantityOut = g.Sum(m => m.Direction == StockDirection.Out ? m.Quantity : 0m)
            })
            .ToListAsync(ct);

        // 4. Fetch item details
        var itemIds = movementsSummary.Select(x => x.ItemId).Distinct().ToList();

        var itemsQuery = from i in _db.Items
                         where itemIds.Contains(i.ItemId)
                         join c in _db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
                         from c in categories.DefaultIfEmpty()
                         join u in _db.UnitsOfMeasure on i.InventoryUomId equals u.UomId into uoms
                         from u in uoms.DefaultIfEmpty()
                         select new
                         {
                             i.ItemId,
                             i.ItemCode,
                             i.ItemName,
                             i.ItemCategoryId,
                             CategoryName = c != null ? c.CategoryName : null,
                             UnitCode = u != null ? u.UomCode : null,
                             i.PurchasePrice
                         };

        if (request.ItemCategoryId is long reqCatId)
        {
            itemsQuery = itemsQuery.Where(i => i.ItemCategoryId == reqCatId);
        }

        var itemsMap = await itemsQuery.ToDictionaryAsync(i => i.ItemId, ct);

        string? searchTerm = !string.IsNullOrWhiteSpace(request.Search) ? request.Search.Trim().ToLowerInvariant() : null;

        // 5. Build warehouse groups
        List<WarehouseTrackingGroupDto> groups = [];

        foreach (var w in warehouses)
        {
            string? stateName = w.StateId is int sid && states.TryGetValue(sid, out string? sn) ? sn : null;
            string? countryName = w.CountryId is int cid && countries.TryGetValue(cid, out string? cn) ? cn : null;

            var warehouseMovements = movementsSummary.Where(m => m.WarehouseId == w.WarehouseId).ToList();

            List<WarehouseTrackingItemDto> warehouseItems = [];

            foreach (var wm in warehouseMovements)
            {
                if (!itemsMap.TryGetValue(wm.ItemId, out var item))
                {
                    continue;
                }

                decimal available = wm.QuantityIn - wm.QuantityOut;
                if (!request.ShowZeroBalance && available == 0m && wm.QuantityIn == 0m)
                {
                    continue;
                }

                if (searchTerm != null)
                {
                    bool match = item.ItemCode.ToLowerInvariant().Contains(searchTerm) ||
                                 item.ItemName.ToLowerInvariant().Contains(searchTerm) ||
                                 w.WarehouseName.ToLowerInvariant().Contains(searchTerm);
                    if (!match)
                    {
                        continue;
                    }
                }

                decimal avgCost = branchAverageCosts.TryGetValue(wm.ItemId, out decimal c)
                    ? c
                    : (item.PurchasePrice ?? 0m);

                decimal estVal = Math.Round(available * avgCost, 2, MidpointRounding.AwayFromZero);

                warehouseItems.Add(new WarehouseTrackingItemDto
                {
                    ItemId = item.ItemId,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    CategoryName = item.CategoryName,
                    UnitCode = item.UnitCode,
                    QuantityIn = wm.QuantityIn,
                    QuantityOut = wm.QuantityOut,
                    AvailableQuantity = available,
                    AverageCost = avgCost,
                    EstimatedValue = estVal
                });
            }

            warehouseItems = warehouseItems.OrderBy(i => i.ItemName).ToList();

            // If a search term was specified, skip warehouses that have 0 matching items unless warehouse name matched
            if (searchTerm != null && warehouseItems.Count == 0 && !w.WarehouseName.ToLowerInvariant().Contains(searchTerm))
            {
                continue;
            }

            var group = new WarehouseTrackingGroupDto
            {
                WarehouseId = w.WarehouseId,
                WarehouseCode = w.WarehouseCode,
                WarehouseName = w.WarehouseName,
                Address = w.AddressLine1,
                City = w.City,
                State = stateName,
                Country = countryName,
                IsPrimary = w.IsDefault,
                IsActive = w.IsActive,
                TotalItems = warehouseItems.Count,
                TotalQuantityIn = warehouseItems.Sum(i => i.QuantityIn),
                TotalQuantityOut = warehouseItems.Sum(i => i.QuantityOut),
                TotalAvailableQuantity = warehouseItems.Sum(i => i.AvailableQuantity),
                TotalEstimatedValue = warehouseItems.Sum(i => i.EstimatedValue),
                Items = warehouseItems
            };

            groups.Add(group);
        }

        // Totals
        var totals = new WarehouseTrackingTotalsDto
        {
            TotalWarehouses = groups.Count,
            TotalItems = groups.Sum(g => g.TotalItems),
            TotalQuantityIn = groups.Sum(g => g.TotalQuantityIn),
            TotalQuantityOut = groups.Sum(g => g.TotalQuantityOut),
            TotalAvailableQuantity = groups.Sum(g => g.TotalAvailableQuantity),
            TotalEstimatedValue = groups.Sum(g => g.TotalEstimatedValue)
        };

        var pagedGroups = groups
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new WarehouseTrackingStatusReportResultDto
        {
            AsOfDate = asOf,
            Currency = currency,
            Warehouses = pagedGroups,
            Totals = totals,
            TotalWarehousesCount = groups.Count,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}

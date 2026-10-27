using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Batch Tracking Status Report (TK-247): balances by batch as at a date.
/// Tracks received and available quantities per warehouse, with manufacture and expiry status.
/// FEFO items ordered by expiry date first.
/// </summary>
public sealed class BatchTrackingStatusReportRepository
{
    private readonly ReportingDbContext _db;

    public BatchTrackingStatusReportRepository(ReportingDbContext db)
    {
        _db = db;
    }

    /// <summary>Refusals for missing required fields.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(BatchTrackingStatusReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (!request.AsOfDate.HasValue)
        {
            refusals.Add((nameof(BatchTrackingStatusReportRequest.AsOfDate), "As at date is required."));
        }

        return refusals;
    }

    /// <summary>Validates item and warehouse scope if specified.</summary>
    public async Task<IReadOnlyList<(string Field, string Message)>> ValidateScopeAsync(
        BatchTrackingStatusReportRequest request,
        CancellationToken cancellationToken = default)
    {
        List<(string, string)> refusals = [];

        if (request.ItemId.HasValue)
        {
            bool exists = await _db.Items.AsNoTracking()
                .AnyAsync(i => i.ItemId == request.ItemId.Value, cancellationToken);
            if (!exists)
            {
                refusals.Add((nameof(BatchTrackingStatusReportRequest.ItemId), "Selected item does not exist."));
            }
        }

        if (request.WarehouseId.HasValue)
        {
            bool exists = await _db.Warehouses.AsNoTracking()
                .AnyAsync(w => w.WarehouseId == request.WarehouseId.Value, cancellationToken);
            if (!exists)
            {
                refusals.Add((nameof(BatchTrackingStatusReportRequest.WarehouseId), "Selected warehouse does not exist."));
            }
        }

        return refusals;
    }

    /// <summary>Fetches the Batch Tracking Status Report.</summary>
    public async Task<BatchTrackingStatusReportDto> GetReportAsync(
        BatchTrackingStatusReportRequest request,
        CancellationToken cancellationToken = default)
    {
        DateOnly asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        int asOfDay = asOf.DayNumber;

        IQueryable<StockMovementRead> movesQuery = _db.StockMovements.AsNoTracking()
            .Where(m => m.ItemBatchId != null && m.MovementDate <= asOf);

        if (request.WarehouseId.HasValue)
        {
            movesQuery = movesQuery.Where(m => m.WarehouseId == request.WarehouseId.Value);
        }

        var batchMoves = movesQuery
            .GroupBy(m => new { ItemBatchId = m.ItemBatchId!.Value, m.WarehouseId })
            .Select(g => new
            {
                g.Key.ItemBatchId,
                g.Key.WarehouseId,
                ReceivedQuantity = g.Where(x => x.Direction == StockDirection.In).Sum(x => x.Quantity),
                AvailableQuantity = g.Sum(x => x.Direction == StockDirection.In ? x.Quantity : -x.Quantity)
            });

        var query = from m in batchMoves
                    join b in _db.ItemBatches.AsNoTracking() on m.ItemBatchId equals b.ItemBatchId
                    join i in _db.Items.AsNoTracking() on b.ItemId equals i.ItemId
                    join c in _db.ItemCategories.AsNoTracking() on i.ItemCategoryId equals c.ItemCategoryId into cats
                    from c in cats.DefaultIfEmpty()
                    join u in _db.UnitsOfMeasure.AsNoTracking() on i.InventoryUomId equals u.UomId into uoms
                    from u in uoms.DefaultIfEmpty()
                    join w in _db.Warehouses.AsNoTracking() on m.WarehouseId equals w.WarehouseId into whs
                    from w in whs.DefaultIfEmpty()
                    select new
                    {
                        b.ItemBatchId,
                        b.BatchNumber,
                        b.ItemId,
                        i.ItemCode,
                        i.ItemName,
                        CategoryName = c != null ? c.CategoryName : null,
                        UnitCode = u != null ? u.UomCode : null,
                        b.ManufactureDate,
                        b.ExpiryDate,
                        CostingMethod = i.CostingType.ToString(),
                        m.WarehouseId,
                        WarehouseName = w != null ? w.WarehouseName : "Unknown",
                        m.ReceivedQuantity,
                        m.AvailableQuantity,
                        b.Mrp
                    };

        if (request.ItemId.HasValue)
        {
            query = query.Where(x => x.ItemId == request.ItemId.Value);
        }

        if (request.WithStockOnly)
        {
            query = query.Where(x => x.AvailableQuantity > 0m);
        }

        if (request.ExpiredOnly)
        {
            query = query.Where(x => x.ExpiryDate != null && x.ExpiryDate.Value < asOf);
        }
        else if (request.ExpiringWithinDays.HasValue)
        {
            int maxDays = asOfDay + request.ExpiringWithinDays.Value;
            query = query.Where(x => x.ExpiryDate != null && x.ExpiryDate.Value.DayNumber >= asOfDay && x.ExpiryDate.Value.DayNumber <= maxDays);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string term = request.Search.Trim().ToLower();
            query = query.Where(x => x.BatchNumber.ToLower().Contains(term)
                                  || x.ItemCode.ToLower().Contains(term)
                                  || x.ItemName.ToLower().Contains(term));
        }

        int totalItems = await query.CountAsync(cancellationToken);

        var totalsAgg = totalItems > 0
            ? await query.GroupBy(_ => 1).Select(g => new
            {
                TotalReceivedQuantity = g.Sum(x => x.ReceivedQuantity),
                TotalAvailableQuantity = g.Sum(x => x.AvailableQuantity),
                ExpiredCount = g.Sum(x => x.ExpiryDate != null && x.ExpiryDate.Value < asOf ? 1 : 0),
                NearExpiryCount = g.Sum(x => x.ExpiryDate != null && x.ExpiryDate.Value.DayNumber >= asOfDay && x.ExpiryDate.Value.DayNumber <= asOfDay + 30 ? 1 : 0)
            }).FirstOrDefaultAsync(cancellationToken)
            : null;

        var totals = new BatchTrackingStatusTotalsDto
        {
            TotalReceivedQuantity = totalsAgg?.TotalReceivedQuantity ?? 0m,
            TotalAvailableQuantity = totalsAgg?.TotalAvailableQuantity ?? 0m,
            ExpiredBatchCount = totalsAgg?.ExpiredCount ?? 0,
            NearExpiryBatchCount = totalsAgg?.NearExpiryCount ?? 0
        };

        int pageNumber = Math.Max(1, request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 200);

        var orderedQuery = query
            .OrderBy(x => x.ExpiryDate == null)
            .ThenBy(x => x.ExpiryDate)
            .ThenBy(x => x.ItemCode)
            .ThenBy(x => x.BatchNumber);

        var pageRows = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pageRows.Select(r =>
        {
            int? days = r.ExpiryDate.HasValue ? r.ExpiryDate.Value.DayNumber - asOfDay : null;
            bool expired = days.HasValue && days.Value < 0;
            bool nearExpiry = days.HasValue && days.Value >= 0 && days.Value <= 30;

            return new BatchTrackingStatusItemDto
            {
                ItemBatchId = r.ItemBatchId,
                BatchNumber = r.BatchNumber,
                ItemId = r.ItemId,
                ItemCode = r.ItemCode,
                ItemName = r.ItemName,
                CategoryName = r.CategoryName,
                UnitCode = r.UnitCode,
                ManufactureDate = r.ManufactureDate,
                ExpiryDate = r.ExpiryDate,
                CostingMethod = r.CostingMethod,
                WarehouseId = r.WarehouseId,
                WarehouseName = r.WarehouseName,
                ReceivedQuantity = r.ReceivedQuantity,
                AvailableQuantity = r.AvailableQuantity,
                Mrp = r.Mrp,
                DaysToExpiry = days,
                IsExpired = expired,
                IsNearExpiry = nearExpiry
            };
        }).ToList();

        return new BatchTrackingStatusReportDto
        {
            AsOfDate = asOf,
            Items = items,
            TotalItems = totalItems,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Totals = totals
        };
    }

    /// <summary>Items that have tracked batches.</summary>
    public async Task<IReadOnlyList<BatchTrackingItemChoice>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        return await (from b in _db.ItemBatches.AsNoTracking()
                      join i in _db.Items.AsNoTracking() on b.ItemId equals i.ItemId
                      select new { i.ItemId, i.ItemCode, i.ItemName })
                     .Distinct()
                     .OrderBy(i => i.ItemName)
                     .Select(i => new BatchTrackingItemChoice
                     {
                         ItemId = i.ItemId,
                         ItemCode = i.ItemCode,
                         ItemName = i.ItemName
                     })
                     .ToListAsync(cancellationToken);
    }

    /// <summary>Active warehouses.</summary>
    public async Task<IReadOnlyList<BatchTrackingWarehouseChoice>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Warehouses.AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.WarehouseName)
            .Select(w => new BatchTrackingWarehouseChoice
            {
                WarehouseId = w.WarehouseId,
                WarehouseCode = w.WarehouseCode,
                WarehouseName = w.WarehouseName
            })
            .ToListAsync(cancellationToken);
    }
}

using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Api.Services;

/// <summary>
/// Stock: one pool per item, company-wide, and the append-only movement history
/// behind it.
///
/// The decrement is the part that matters. It is a single conditional UPDATE —
/// <c>SET QuantityOnHand = QuantityOnHand - @qty WHERE QuantityOnHand &gt;= @qty</c>
/// — and the row count is the answer. Zero rows means there was not enough and
/// nothing changed. Read-then-write would let two tills both see the last unit
/// and both sell it, which is exactly the failure this shape exists to prevent.
/// It is synchronous for the same reason: costing, accounting and notifications
/// can all be told afterwards, but the quantity cannot.
/// </summary>
public sealed class StockService
{
    private readonly InventoryDbContext _db;
    private readonly CostingService _costing;
    private readonly TimeProvider _clock;

    public StockService(InventoryDbContext db, CostingService costing, TimeProvider clock)
    {
        _db = db;
        _costing = costing;
        _clock = clock;
    }

    /// <summary>Quantities are held to three decimals; the movement check constraint asserts the same.</summary>
    private const int QuantityScale = 3;

    public async Task<StockPosition?> GetAsync(long itemId, CancellationToken ct)
    {
        List<StockPosition> rows = await QueryPositions(_db.Items.Where(i => i.ItemId == itemId))
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return null;
        }

        StockPosition position = Finish(rows[0]);

        // Only for one item: summing layers across a whole list would be a
        // second scan of the largest table here for a figure the list does not
        // show.
        if (position.UsesCostLayers)
        {
            position.LayeredStockValue = Math.Round(
                await _costing.LayeredValueAsync(itemId, ct), 2, MidpointRounding.AwayFromZero);
        }

        return position;
    }

    /// <summary>
    /// Every stocked item, whether or not it has a stock row yet — an item that
    /// has never moved is a zero, not an absence, and a reorder report that
    /// silently skipped it would be worse than useless.
    /// </summary>
    public async Task<IReadOnlyList<StockPosition>> ListAsync(
        string? search, bool belowReorderOnly, CancellationToken ct)
    {
        IQueryable<Item> items = _db.Items.Where(i => i.IsActive && i.TrackInventory);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            items = items.Where(i =>
                EF.Functions.ILike(i.ItemName, $"%{term}%")
                || EF.Functions.ILike(i.ItemCode, $"%{term}%"));
        }

        List<StockPosition> rows = await QueryPositions(items)
            .OrderBy(p => p.ItemName)
            .ToListAsync(ct);

        IEnumerable<StockPosition> finished = rows.Select(Finish);

        return belowReorderOnly
            ? finished.Where(p => p.IsBelowReorderLevel).ToList()
            : finished.ToList();
    }

    public async Task<IReadOnlyList<StockMovementListItem>> ListMovementsAsync(
        long? itemId, long? warehouseId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        IQueryable<StockMovement> query = _db.StockMovements;

        if (itemId is long item)
        {
            query = query.Where(m => m.ItemId == item);
        }

        if (warehouseId is long warehouse)
        {
            query = query.Where(m => m.WarehouseId == warehouse);
        }

        if (from is DateOnly start)
        {
            query = query.Where(m => m.MovementDate >= start);
        }

        if (to is DateOnly end)
        {
            query = query.Where(m => m.MovementDate <= end);
        }

        return await query
            .Join(_db.Items, m => m.ItemId, i => i.ItemId, (m, i) => new { m, i })
            .Join(_db.UnitOfMeasures, x => x.m.EnteredUomId, u => u.UomId, (x, u) => new { x.m, x.i, u })
            .Select(x => new StockMovementListItem
            {
                StockMovementId = x.m.StockMovementId,
                ItemId = x.m.ItemId,
                ItemCode = x.i.ItemCode,
                ItemName = x.i.ItemName,
                WarehouseId = x.m.WarehouseId,
                WarehouseName = _db.Warehouses
                    .Where(w => w.WarehouseId == x.m.WarehouseId)
                    .Select(w => w.WarehouseName)
                    .FirstOrDefault(),
                MovementType = x.m.MovementType.ToString(),
                Direction = x.m.Direction.ToString(),
                MovementDate = x.m.MovementDate,
                EnteredQuantity = x.m.EnteredQuantity,
                EnteredUomId = x.m.EnteredUomId,
                EnteredUomCode = x.u.UomCode,
                Quantity = x.m.Quantity,
                ConversionFactor = x.m.ConversionFactor,
                UnitCost = x.m.UnitCost,
                TotalCost = x.m.TotalCost,
                ResultingWeightedAverageCost = x.m.ResultingWeightedAverageCost,
                SourceType = x.m.SourceType,
                SourceId = x.m.SourceId,
                ReturnsStockMovementId = x.m.ReturnsStockMovementId,
                CostingStatus = x.m.CostingStatus.ToString(),
                CostedAt = x.m.CostedAt,
                CostingError = x.m.CostingError,
                Notes = x.m.Notes,
                RecordedAt = x.m.CreatedAt,
            })
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.StockMovementId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// How far behind costing is. Worth showing rather than hiding: an
    /// asynchronous cost that silently stopped settling would leave every
    /// margin report quietly wrong.
    /// </summary>
    public async Task<CostingQueueStatus> CostingQueueAsync(CancellationToken ct)
    {
        var counts = await _db.StockMovements
            .Where(m => m.CostingStatus == CostingStatus.Pending
                || m.CostingStatus == CostingStatus.InProgress
                || m.CostingStatus == CostingStatus.Failed)
            .GroupBy(m => m.CostingStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        DateTimeOffset? oldest = await _db.StockMovements
            .Where(m => m.CostingStatus == CostingStatus.Pending)
            .MinAsync(m => (DateTimeOffset?)m.CreatedAt, ct);

        return new CostingQueueStatus
        {
            Pending = counts.FirstOrDefault(c => c.Status == CostingStatus.Pending)?.Count ?? 0,
            InProgress = counts.FirstOrDefault(c => c.Status == CostingStatus.InProgress)?.Count ?? 0,
            Failed = counts.FirstOrDefault(c => c.Status == CostingStatus.Failed)?.Count ?? 0,
            OldestPendingAt = oldest,
        };
    }

    /// <summary>What an issue took, layer by layer. Empty on a weighted-average item.</summary>
    public Task<IReadOnlyList<CostAllocationItem>> AllocationsAsync(
        long stockMovementId, CancellationToken ct) =>
        _costing.AllocationsAsync(stockMovementId, ct);

    /// <summary>
    /// Costs restated by a backdated receipt — which sale, by how much, and
    /// because of which receipt.
    /// </summary>
    public async Task<IReadOnlyList<RecostingAdjustmentItem>> RecostingsAsync(
        long? itemId, CancellationToken ct)
    {
        IQueryable<RecostingAdjustment> query = _db.RecostingAdjustments;

        if (itemId is long id)
        {
            query = query.Where(a => a.ItemId == id);
        }

        return await query
            .Join(_db.Items, a => a.ItemId, i => i.ItemId, (a, i) => new { a, i })
            .Select(x => new RecostingAdjustmentItem
            {
                RecostingAdjustmentId = x.a.RecostingAdjustmentId,
                RecostingBatchId = x.a.RecostingBatchId,
                ItemId = x.a.ItemId,
                ItemCode = x.i.ItemCode,
                ItemName = x.i.ItemName,
                StockMovementId = x.a.StockMovementId,
                TriggerStockMovementId = x.a.TriggerStockMovementId,
                MovementDate = _db.StockMovements
                    .Where(m => m.StockMovementId == x.a.StockMovementId)
                    .Select(m => m.MovementDate)
                    .FirstOrDefault(),
                PreviousCost = x.a.PreviousCost,
                NewCost = x.a.NewCost,
                Delta = x.a.Delta,
                RunAt = x.a.RunAt,
            })
            .OrderByDescending(a => a.RunAt)
            .ThenBy(a => a.MovementDate)
            .ToListAsync(ct);
    }

    public async Task<RecordStockMovementResult> RecordAsync(
        RecordStockMovementRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.MovementType, ignoreCase: true, out StockMovementType type))
        {
            return Fail(StockOutcome.InvalidValue);
        }

        Item? item = await _db.Items.FirstOrDefaultAsync(i => i.ItemId == request.ItemId, ct);
        if (item is null)
        {
            return Fail(StockOutcome.ItemNotFound);
        }

        if (!item.TrackInventory)
        {
            return Fail(StockOutcome.NotStocked);
        }

        StockDirection direction = DirectionOf(type, request.Direction);

        (StockOutcome conversionOutcome, decimal factor) =
            await ResolveFactorAsync(item, request.UomId, ct);

        if (conversionOutcome != StockOutcome.Ok)
        {
            return Fail(conversionOutcome);
        }

        if (request.WarehouseId is long warehouseId
            && !await _db.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId, ct))
        {
            return Fail(StockOutcome.UnknownWarehouse);
        }

        // Bringing stock in without a cost would drag weighted average cost
        // toward zero and understate every COGS posting after it.
        bool movesCost = MovesWeightedAverage(type);
        if (movesCost && request.UnitCost is null)
        {
            return Fail(StockOutcome.UnitCostRequired);
        }

        if (request.SourceType is { Length: > 0 } sourceType
            && request.SourceId is long sourceId
            && await _db.StockMovements.AnyAsync(
                m => m.SourceType == sourceType
                    && m.SourceId == sourceId
                    && m.SourceLineId == request.SourceLineId, ct))
        {
            // The unique index would catch this too. Checking first turns a
            // redelivered event into a clean "already done" instead of a 500.
            return Fail(StockOutcome.DuplicateSource);
        }

        StockOutcome tracking = ValidateTracking(item, direction, request);
        if (tracking != StockOutcome.Ok)
        {
            return Fail(tracking);
        }

        decimal quantity = Math.Round(
            request.Quantity * factor, QuantityScale, MidpointRounding.AwayFromZero);

        if (quantity <= 0)
        {
            // A quantity small enough to round away is not a movement — and the
            // check constraint would refuse it anyway.
            return Fail(StockOutcome.InvalidValue);
        }

        // Cost per inventory unit, from a cost per entered unit.
        decimal? unitCost = request.UnitCost is decimal entered ? entered / factor : null;

        return await ApplyAsync(item, type, direction, quantity, factor, unitCost, request, ct);
    }

    /// <summary>
    /// Warehouse to warehouse. Two movements and no net change: the pool is
    /// shared, so a transfer records where stock sits and nothing else. It is
    /// still refused when the source has less than the quantity, because a
    /// warehouse cannot ship what it does not hold.
    /// </summary>
    public async Task<RecordStockMovementResult> TransferAsync(
        TransferStockRequest request, CancellationToken ct)
    {
        if (request.FromWarehouseId == request.ToWarehouseId)
        {
            return Fail(StockOutcome.SameWarehouse);
        }

        var outbound = new RecordStockMovementRequest
        {
            ItemId = request.ItemId,
            MovementType = nameof(StockMovementType.TransferOut),
            MovementDate = request.MovementDate,
            Quantity = request.Quantity,
            UomId = request.UomId,
            WarehouseId = request.FromWarehouseId,
            Notes = request.Notes,
        };

        RecordStockMovementResult sent = await RecordAsync(outbound, ct);
        if (sent.Outcome != StockOutcome.Ok)
        {
            return sent;
        }

        var inbound = new RecordStockMovementRequest
        {
            ItemId = request.ItemId,
            MovementType = nameof(StockMovementType.TransferIn),
            MovementDate = request.MovementDate,
            Quantity = request.Quantity,
            UomId = request.UomId,
            WarehouseId = request.ToWarehouseId,
            Notes = request.Notes,
        };

        return await RecordAsync(inbound, ct);
    }

    /// <summary>
    /// Writes the movement and moves the pool, in one transaction. The two have
    /// to commit together: a movement without the balance change makes the
    /// ledger disagree with the position, and the reverse loses the audit trail.
    /// </summary>
    private async Task<RecordStockMovementResult> ApplyAsync(
        Item item,
        StockMovementType type,
        StockDirection direction,
        decimal quantity,
        decimal factor,
        decimal? unitCost,
        RecordStockMovementRequest request,
        CancellationToken ct)
    {
        await using IDbContextTransaction tx = await _db.Database.BeginTransactionAsync(ct);

        decimal? resultingCost = null;

        if (AffectsPool(type))
        {
            if (direction == StockDirection.Out)
            {
                // The whole point. One statement, guarded, and the row count is
                // the answer — never a read followed by a write.
                int moved = await _db.ItemStock
                    .Where(s => s.ItemId == item.ItemId && s.QuantityOnHand >= quantity)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(x => x.QuantityOnHand, x => x.QuantityOnHand - quantity),
                        ct);

                if (moved == 0)
                {
                    // No row, or not enough in it. Either way nothing changed.
                    await tx.RollbackAsync(ct);
                    return Fail(StockOutcome.InsufficientStock);
                }
            }
            else
            {
                await IncreaseAsync(item.ItemId, quantity, unitCost, MovesWeightedAverage(type), ct);
            }
        }
        else if (direction == StockDirection.Out)
        {
            // A transfer out still cannot ship more than exists, even though the
            // pool is unchanged by it.
            bool enough = await _db.ItemStock
                .AnyAsync(s => s.ItemId == item.ItemId && s.QuantityOnHand >= quantity, ct);

            if (!enough)
            {
                await tx.RollbackAsync(ct);
                return Fail(StockOutcome.InsufficientStock);
            }
        }

        // Read back through a fresh query, not the change tracker: ExecuteUpdate
        // changed the row in the database and left any tracked instance holding
        // the numbers it had before.
        var after = await _db.ItemStock
            .AsNoTracking()
            .Where(s => s.ItemId == item.ItemId)
            .Select(s => new { s.QuantityOnHand, s.WeightedAverageCost })
            .FirstOrDefaultAsync(ct);

        resultingCost = after?.WeightedAverageCost;

        // Anything that does not set the cost is valued at the cost already
        // held, so an issue, a return and an adjustment all leave valuation
        // where the receipts put it.
        decimal? movementCost = unitCost ?? resultingCost;

        var movement = new StockMovement
        {
            ItemId = item.ItemId,
            WarehouseId = request.WarehouseId,
            MovementType = type,
            Direction = direction,
            MovementDate = request.MovementDate
                ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime),
            EnteredQuantity = request.Quantity,
            EnteredUomId = request.UomId ?? item.InventoryUomId,
            Quantity = quantity,
            ConversionFactor = factor,
            UnitCost = movementCost,
            TotalCost = movementCost is decimal cost
                ? Math.Round(quantity * cost, 2, MidpointRounding.AwayFromZero)
                : null,
            ResultingWeightedAverageCost = resultingCost,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            SourceLineId = request.SourceLineId,
            ReturnsStockMovementId = request.ReturnsStockMovementId,
            Notes = request.Notes,
        };

        _db.StockMovements.Add(movement);
        await _db.SaveChangesAsync(ct);

        // Batch and serial handling stays here: both are user input, and a bad
        // batch number or a serial that is not on the shelf has to fail the save
        // rather than surface later as a costing error nobody is watching.
        (StockOutcome prepared, long? batchId) =
            await PrepareTrackingAsync(item, movement, direction, request, ct);

        if (prepared != StockOutcome.Ok)
        {
            await tx.RollbackAsync(ct);
            return Fail(prepared);
        }

        // Costing itself is the worker's. The quantity has moved and the caller
        // can be answered; what it cost is settled shortly afterwards, and the
        // status on the row is what says so rather than a null cost that could
        // be mistaken for free stock.
        movement.ItemBatchId = batchId;
        movement.CostingStatus = NeedsCosting(item, type, direction)
            ? CostingStatus.Pending
            : CostingStatus.Costed;

        await _db.SaveChangesAsync(ct);

        if (after is not null)
        {
            // Also a statement rather than a tracked edit, for the same reason.
            await _db.ItemStock
                .Where(s => s.ItemId == item.ItemId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.LastMovementAt, _clock.GetUtcNow()), ct);
        }

        await tx.CommitAsync(ct);

        return new RecordStockMovementResult(
            StockOutcome.Ok, movement.StockMovementId, await GetAsync(item.ItemId, ct));
    }

    /// <summary>
    /// The item's tracking flags, checked against what the movement supplied.
    /// These live on the item, so no database constraint can see them from here.
    /// </summary>
    private static StockOutcome ValidateTracking(
        Item item, StockDirection direction, RecordStockMovementRequest request)
    {
        bool inbound = direction == StockDirection.In;

        if (item.IsBatchTracked
            && inbound
            && request.ItemBatchId is null
            && string.IsNullOrWhiteSpace(request.BatchNumber))
        {
            return StockOutcome.BatchRequired;
        }

        // Only when the lot is new: an existing lot already carries its date.
        if (item.IsExpiryTracked
            && inbound
            && request.ItemBatchId is null
            && request.BatchExpiryDate is null)
        {
            return StockOutcome.ExpiryRequired;
        }

        if (item.IsSerialTracked)
        {
            // One serial per unit, exactly. Fewer and pieces go untracked; more
            // and the count disagrees with the quantity that moved.
            decimal expected = Math.Round(
                request.Quantity, QuantityScale, MidpointRounding.AwayFromZero);

            if (request.SerialNumbers.Count != (int)expected || expected != (int)expected)
            {
                return StockOutcome.SerialCountMismatch;
            }

            if (request.SerialNumbers.Any(string.IsNullOrWhiteSpace)
                || request.SerialNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    != request.SerialNumbers.Count)
            {
                return StockOutcome.SerialConflict;
            }
        }

        return StockOutcome.Ok;
    }

    /// <summary>
    /// Resolves the lot and moves the serials, inside the request. Both are
    /// user input: a batch number that does not exist, or a serial that is not
    /// on the shelf, is the caller's mistake and has to be answered as one.
    ///
    /// Layers are deliberately not touched here — that is the worker's.
    /// </summary>
    private async Task<(StockOutcome Outcome, long? BatchId)> PrepareTrackingAsync(
        Item item,
        StockMovement movement,
        StockDirection direction,
        RecordStockMovementRequest request,
        CancellationToken ct)
    {
        if (!AffectsPool(movement.MovementType))
        {
            return (StockOutcome.Ok, null);
        }

        (StockOutcome batchOutcome, ItemBatch? batch) = await ResolveBatchAsync(item, request, ct);
        if (batchOutcome != StockOutcome.Ok)
        {
            return (batchOutcome, null);
        }

        if (direction == StockDirection.In)
        {
            if (IsReturn(movement.MovementType)
                && request.ReturnsStockMovementId is long returnedId)
            {
                StockOutcome valid = await ValidateReturnedMovementAsync(item, returnedId, ct);
                if (valid != StockOutcome.Ok)
                {
                    return (valid, null);
                }
            }

            // Serials are created without a layer id — the layer does not exist
            // yet. The worker points them at it once it does.
            StockOutcome created = await CreateSerialsAsync(item, movement, batch, request, ct);
            return created == StockOutcome.Ok
                ? (StockOutcome.Ok, batch?.ItemBatchId)
                : (created, null);
        }

        (StockOutcome retired, List<long> _) =
            await RetireSerialsAsync(item, movement, request, ct);

        return retired == StockOutcome.Ok
            ? (StockOutcome.Ok, batch?.ItemBatchId)
            : (retired, null);
    }

    /// <summary>
    /// Whether the worker has anything to do. A transfer moves nothing to cost,
    /// and an issue on weighted average is already costed by the running average
    /// — marking those Pending would leave a queue that never drains.
    /// </summary>
    private static bool NeedsCosting(
        Item item, StockMovementType type, StockDirection direction)
    {
        if (!AffectsPool(type))
        {
            return false;
        }

        // Inbound always creates a layer, whatever the method: the layer is the
        // receipt, and one never written could not be reconstructed later.
        return direction == StockDirection.In
            || CostingService.ConsumesLayers(item.CostingType);
    }

    /// <summary>Only a return can name a movement it reverses.</summary>
    private static bool IsReturn(StockMovementType type) =>
        type is StockMovementType.SalesReturn or StockMovementType.PurchaseReturn;

    /// <summary>
    /// The movement being returned has to be an outbound movement of this same
    /// item. Anything else and the layers found would belong to something else.
    /// </summary>
    private async Task<StockOutcome> ValidateReturnedMovementAsync(
        Item item, long returnedMovementId, CancellationToken ct)
    {
        bool valid = await _db.StockMovements.AnyAsync(
            m => m.StockMovementId == returnedMovementId
                && m.ItemId == item.ItemId
                && m.Direction == StockDirection.Out,
            ct);

        return valid ? StockOutcome.Ok : StockOutcome.ReturnedMovementNotFound;
    }

    /// <summary>
    /// Finds the lot by id, or by number, creating it on the way in. A lot named
    /// on an issue must already exist — inventing one there would be inventing
    /// stock.
    /// </summary>
    private async Task<(StockOutcome Outcome, ItemBatch? Batch)> ResolveBatchAsync(
        Item item, RecordStockMovementRequest request, CancellationToken ct)
    {
        if (request.ItemBatchId is long batchId)
        {
            ItemBatch? existing = await _db.ItemBatches
                .FirstOrDefaultAsync(b => b.ItemBatchId == batchId && b.ItemId == item.ItemId, ct);

            return existing is null
                ? (StockOutcome.BatchNotFound, null)
                : (StockOutcome.Ok, existing);
        }

        if (string.IsNullOrWhiteSpace(request.BatchNumber))
        {
            return (StockOutcome.Ok, null);
        }

        string number = request.BatchNumber.Trim();

        ItemBatch? found = await _db.ItemBatches
            .FirstOrDefaultAsync(b => b.ItemId == item.ItemId && b.BatchNumber == number, ct);

        if (found is not null)
        {
            return (StockOutcome.Ok, found);
        }

        var batch = new ItemBatch
        {
            ItemId = item.ItemId,
            BatchNumber = number,
            ManufactureDate = request.BatchManufactureDate,
            ExpiryDate = request.BatchExpiryDate,
            Mrp = request.BatchMrp,
            IsActive = true,
        };

        _db.ItemBatches.Add(batch);
        await _db.SaveChangesAsync(ct);

        return (StockOutcome.Ok, batch);
    }

    /// <summary>
    /// Creates one serial per unit received. The layer they arrived on is
    /// attached by the worker, because it does not exist yet — costing runs
    /// after the request has been answered.
    /// </summary>
    private async Task<StockOutcome> CreateSerialsAsync(
        Item item,
        StockMovement movement,
        ItemBatch? batch,
        RecordStockMovementRequest request,
        CancellationToken ct)
    {
        if (!item.IsSerialTracked || request.SerialNumbers.Count == 0)
        {
            return StockOutcome.Ok;
        }

        string[] numbers = request.SerialNumbers.Select(s => s.Trim()).ToArray();

        // A serial number is unique per item, so a repeat is a re-entry of a
        // piece already in stock rather than a second piece.
        if (await _db.ItemSerials.AnyAsync(
                s => s.ItemId == item.ItemId && numbers.Contains(s.SerialNumber), ct))
        {
            return StockOutcome.SerialConflict;
        }

        for (int i = 0; i < numbers.Length; i++)
        {
            _db.ItemSerials.Add(new ItemSerial
            {
                ItemId = item.ItemId,
                SerialNumber = numbers[i],
                ItemBatchId = batch?.ItemBatchId,
                HallmarkNumber = i < request.HallmarkNumbers.Count
                    && !string.IsNullOrWhiteSpace(request.HallmarkNumbers[i])
                        ? request.HallmarkNumbers[i].Trim()
                        : null,
                Status = SerialStatus.InStock,
                WarehouseId = movement.WarehouseId,
                ReceivedMovementId = movement.StockMovementId,
            });
        }

        await _db.SaveChangesAsync(ct);
        return StockOutcome.Ok;
    }

    /// <summary>
    /// Marks the named pieces sold and returns the layers they came in on, which
    /// is the whole of specific identification's selection rule.
    /// </summary>
    private async Task<(StockOutcome Outcome, List<long> LayerIds)> RetireSerialsAsync(
        Item item,
        StockMovement movement,
        RecordStockMovementRequest request,
        CancellationToken ct)
    {
        if (!item.IsSerialTracked || request.SerialNumbers.Count == 0)
        {
            return (StockOutcome.Ok, []);
        }

        string[] numbers = request.SerialNumbers.Select(s => s.Trim()).ToArray();

        List<ItemSerial> serials = await _db.ItemSerials
            .Where(s => s.ItemId == item.ItemId
                && numbers.Contains(s.SerialNumber)
                && s.Status == SerialStatus.InStock)
            .ToListAsync(ct);

        // Every named piece has to be on the shelf. A missing one means it was
        // already sold, or never received — either way the sale is wrong.
        if (serials.Count != numbers.Length)
        {
            return (StockOutcome.SerialConflict, []);
        }

        foreach (ItemSerial serial in serials)
        {
            serial.Status = SerialStatus.Sold;
            serial.IssuedMovementId = movement.StockMovementId;
        }

        await _db.SaveChangesAsync(ct);

        return (StockOutcome.Ok, serials
            .Where(s => s.CostLayerId is not null)
            .Select(s => s.CostLayerId!.Value)
            .Distinct()
            .ToList());
    }

    /// <summary>
    /// Adds to the pool, creating the row on first movement.
    ///
    /// The weighted average is recomputed inside the UPDATE rather than in C#:
    /// every SET in one statement reads the row as it was before the statement,
    /// so the old quantity is still the old quantity even though the same
    /// statement is replacing it. Computing it here instead would mean a read,
    /// and a read is where the race lives.
    /// </summary>
    private async Task IncreaseAsync(
        long itemId, decimal quantity, decimal? unitCost, bool movesCost, CancellationToken ct)
    {
        // Hoisted out of the expression tree below. Non-null whenever movesCost
        // is true — RecordAsync refuses the movement otherwise.
        decimal cost = unitCost ?? 0m;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            int moved = movesCost
                ? await _db.ItemStock
                    .Where(s => s.ItemId == itemId)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(
                                x => x.WeightedAverageCost,
                                x => ((x.QuantityOnHand * x.WeightedAverageCost)
                                        + (quantity * cost))
                                    / (x.QuantityOnHand + quantity))
                            .SetProperty(x => x.QuantityOnHand, x => x.QuantityOnHand + quantity),
                        ct)
                : await _db.ItemStock
                    .Where(s => s.ItemId == itemId)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(x => x.QuantityOnHand, x => x.QuantityOnHand + quantity),
                        ct);

            if (moved > 0)
            {
                return;
            }

            // First movement for this item. A concurrent caller may be inserting
            // the same row, so a conflict here is expected rather than an error —
            // the retry then finds the row and updates it.
            var seed = new ItemStock
            {
                ItemId = itemId,
                QuantityOnHand = 0,
                WeightedAverageCost = 0,
            };

            try
            {
                _db.ItemStock.Add(seed);
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Detached rather than clearing the whole tracker: the caller
                // has its own pending work in this context.
                _db.Entry(seed).State = EntityState.Detached;
            }
        }
    }

    /// <summary>
    /// The factor from the entered unit to the item's inventory unit. Both
    /// factors are relative to their type's base unit, so the ratio converts
    /// between them — which is why both units must be of the item's type.
    /// </summary>
    private async Task<(StockOutcome Outcome, decimal Factor)> ResolveFactorAsync(
        Item item, long? enteredUomId, CancellationToken ct)
    {
        if (enteredUomId is not long uomId || uomId == item.InventoryUomId)
        {
            return (StockOutcome.Ok, 1m);
        }

        List<UnitOfMeasure> units = await _db.UnitOfMeasures
            .Where(u => u.UomId == uomId || u.UomId == item.InventoryUomId)
            .ToListAsync(ct);

        UnitOfMeasure? entered = units.FirstOrDefault(u => u.UomId == uomId);
        UnitOfMeasure? inventory = units.FirstOrDefault(u => u.UomId == item.InventoryUomId);

        if (entered is null || inventory is null)
        {
            return (StockOutcome.UnknownUnit, 0m);
        }

        if (entered.UomTypeId != item.UomTypeId || inventory.UomTypeId != item.UomTypeId)
        {
            return (StockOutcome.UnitTypeMismatch, 0m);
        }

        return (StockOutcome.Ok, entered.ConversionToBase / inventory.ConversionToBase);
    }

    /// <summary>
    /// Which way the movement runs. Fixed by the type for everything except an
    /// adjustment, which is the only one that can go either way — a count
    /// correction is as often a shortfall as a surplus.
    /// </summary>
    private static StockDirection DirectionOf(StockMovementType type, string? requested) =>
        type switch
        {
            StockMovementType.Opening => StockDirection.In,
            StockMovementType.Receipt => StockDirection.In,
            StockMovementType.SalesReturn => StockDirection.In,
            StockMovementType.TransferIn => StockDirection.In,
            StockMovementType.Issue => StockDirection.Out,
            StockMovementType.PurchaseReturn => StockDirection.Out,
            StockMovementType.TransferOut => StockDirection.Out,
            _ => Enum.TryParse(requested, ignoreCase: true, out StockDirection parsed)
                ? parsed
                : StockDirection.In,
        };

    /// <summary>
    /// A transfer moves stock between locations, and the pool is shared across
    /// all of them — so it changes nothing to add up. Everything else does.
    /// </summary>
    private static bool AffectsPool(StockMovementType type) =>
        type is not (StockMovementType.TransferIn or StockMovementType.TransferOut);

    /// <summary>
    /// Only a purchase sets what stock cost. A return comes back at what it left
    /// at and an adjustment is a count, not a purchase — valuing either at a new
    /// figure would move the average without anything having been bought.
    /// </summary>
    private static bool MovesWeightedAverage(StockMovementType type) =>
        type is StockMovementType.Opening or StockMovementType.Receipt;

    private IQueryable<StockPosition> QueryPositions(IQueryable<Item> items) =>
        items
            .AsNoTracking()
            .GroupJoin(_db.ItemStock, i => i.ItemId, s => s.ItemId, (i, s) => new { i, s })
            .SelectMany(x => x.s.DefaultIfEmpty(), (x, s) => new { x.i, s })
            .Join(_db.UnitOfMeasures, x => x.i.InventoryUomId, u => u.UomId, (x, u) => new { x.i, x.s, inv = u })
            .Join(_db.UnitOfMeasures, x => x.i.ReportUomId, u => u.UomId, (x, u) => new { x.i, x.s, x.inv, rep = u })
            .Select(x => new StockPosition
            {
                ItemId = x.i.ItemId,
                ItemCode = x.i.ItemCode,
                ItemName = x.i.ItemName,
                // Left join: an item that has never moved has no row, and that
                // is a zero rather than a missing item.
                QuantityOnHand = x.s == null ? 0m : x.s.QuantityOnHand,
                WeightedAverageCost = x.s == null ? 0m : x.s.WeightedAverageCost,
                UomTypeId = x.i.UomTypeId,
                InventoryUomId = x.i.InventoryUomId,
                InventoryUomCode = x.inv.UomCode,
                ReportUomId = x.i.ReportUomId,
                ReportUomCode = x.rep.UomCode,
                // Both factors are relative to the type's base unit, so their
                // ratio converts the stock figure into the report unit.
                QuantityInReportUom = (x.s == null ? 0m : x.s.QuantityOnHand)
                    * x.inv.ConversionToBase / x.rep.ConversionToBase,
                ReorderLevel = x.i.ReorderLevel,
                CostingType = x.i.CostingType.ToString(),
                IsBatchTracked = x.i.IsBatchTracked,
                IsExpiryTracked = x.i.IsExpiryTracked,
                IsSerialTracked = x.i.IsSerialTracked,
                UsesCostLayers = x.i.CostingType == CostingType.Fifo
                    || x.i.CostingType == CostingType.Lifo
                    || x.i.CostingType == CostingType.Fefo
                    || x.i.CostingType == CostingType.SpecificIdentification,
                LastMovementAt = x.s == null ? null : x.s.LastMovementAt,
            });

    /// <summary>Fills the derived fields the query deliberately left to C#.</summary>
    private static StockPosition Finish(StockPosition position)
    {
        position.QuantityInReportUom = Math.Round(
            position.QuantityInReportUom, QuantityScale, MidpointRounding.AwayFromZero);

        position.StockValue = Math.Round(
            position.QuantityOnHand * position.WeightedAverageCost, 2, MidpointRounding.AwayFromZero);

        position.IsBelowReorderLevel =
            position.ReorderLevel is decimal level && position.QuantityOnHand <= level;

        return position;
    }

    private static RecordStockMovementResult Fail(StockOutcome outcome) =>
        new(outcome, null, null);
}

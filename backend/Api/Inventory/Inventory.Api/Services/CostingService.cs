using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Services;

/// <summary>
/// Cost layers: what each receipt cost, and which of them each issue consumed.
///
/// A receipt always creates a layer, whatever the item's costing method — the
/// layer is the receipt, and recording it costs nothing. What differs is
/// consumption:
///
/// <list type="bullet">
/// <item>Weighted average blends every receipt into one number, so it consumes
/// nothing; its layers stand as history rather than as an allocation pool.</item>
/// <item>FIFO takes the oldest receipt first, LIFO the newest.</item>
/// <item>FEFO takes whatever expires soonest, which for a chemist is a legal
/// rule before it is an accounting one.</item>
/// <item>Specific identification takes the layer the named piece came in on, and
/// applies no selection rule at all.</item>
/// </list>
///
/// Layers are decremented the same way stock is: a conditional UPDATE guarded on
/// there being enough, with the row count as the answer. Two sales cannot take
/// the same last unit of a layer.
/// </summary>
public sealed class CostingService
{
    private readonly InventoryDbContext _db;

    public CostingService(InventoryDbContext db) => _db = db;

    /// <summary>Layer quantities share the movement scale, so allocations always sum exactly.</summary>
    private const int QuantityScale = 3;

    /// <summary>How many times a contended layer is retried before giving up.</summary>
    private const int MaxAttempts = 8;

    /// <summary>
    /// Records the receipt as a layer. Called for every inbound movement,
    /// including on weighted-average items — an item's method can never change
    /// once stock has moved, but a layer that was never written could not be
    /// reconstructed if it did.
    /// </summary>
    public CostLayer CreateLayer(
        Item item,
        StockMovement movement,
        long? batchId,
        DateOnly? expiresOn,
        decimal unitCost)
    {
        var layer = new CostLayer
        {
            ItemId = item.ItemId,
            StockMovementId = movement.StockMovementId,
            ItemBatchId = batchId,
            ReceivedOn = movement.MovementDate,
            ExpiresOn = expiresOn,
            OriginalQuantity = movement.Quantity,
            RemainingQuantity = movement.Quantity,
            UnitCost = unitCost,
        };

        _db.CostLayers.Add(layer);
        return layer;
    }

    /// <summary>
    /// Allocates an issue across layers and writes one consumption row per
    /// layer touched. Returns the total cost of the issue — its COGS.
    ///
    /// Returns <see cref="StockOutcome.InsufficientCostLayers"/> when the layers
    /// cannot cover the quantity. That should be impossible, because the stock
    /// decrement has already succeeded and layers and stock move together — so
    /// reaching it means the two have drifted, and failing loudly is the only
    /// safe answer.
    /// </summary>
    public async Task<(StockOutcome Outcome, decimal TotalCost)> ConsumeAsync(
        Item item,
        StockMovement movement,
        IReadOnlyList<long> serialLayerIds,
        CancellationToken ct,
        Guid? recostingBatchId = null)
    {
        if (!ConsumesLayers(item.CostingType))
        {
            return (StockOutcome.Ok, 0m);
        }

        decimal outstanding = movement.Quantity;
        decimal total = 0m;

        for (int attempt = 0; attempt < MaxAttempts && outstanding > 0; attempt++)
        {
            List<CostLayer> candidates = await CandidatesAsync(item, serialLayerIds, ct);

            if (candidates.Count == 0)
            {
                return (StockOutcome.InsufficientCostLayers, 0m);
            }

            foreach (CostLayer layer in candidates)
            {
                if (outstanding <= 0)
                {
                    break;
                }

                decimal take = Math.Min(outstanding, layer.RemainingQuantity);
                if (take <= 0)
                {
                    continue;
                }

                // Guarded, so a layer another sale has already drained affects
                // no rows and is simply skipped on the next pass.
                int taken = await _db.CostLayers
                    .Where(l => l.CostLayerId == layer.CostLayerId && l.RemainingQuantity >= take)
                    .ExecuteUpdateAsync(
                        l => l.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity - take),
                        ct);

                if (taken == 0)
                {
                    continue;
                }

                decimal cost = Math.Round(take * layer.UnitCost, 2, MidpointRounding.AwayFromZero);

                _db.CostLayerConsumptions.Add(new CostLayerConsumption
                {
                    CostLayerId = layer.CostLayerId,
                    StockMovementId = movement.StockMovementId,
                    Quantity = take,
                    UnitCost = layer.UnitCost,
                    TotalCost = cost,
                    RecostingBatchId = recostingBatchId,
                });

                outstanding = Math.Round(
                    outstanding - take, QuantityScale, MidpointRounding.AwayFromZero);
                total += cost;
            }
        }

        return outstanding > 0
            ? (StockOutcome.InsufficientCostLayers, 0m)
            : (StockOutcome.Ok, total);
    }


    /// <summary>
    /// Costs one movement. This is the worker's entry point, and the only
    /// caller: the request path records the movement and leaves.
    ///
    /// Idempotent by construction. The claim is a guarded status change, so a
    /// movement already taken by another worker affects no rows and is left
    /// alone; and the unique index on (issue, layer) refuses a second allocation
    /// even if a claim were somehow duplicated.
    /// </summary>
    public async Task<StockOutcome> CostMovementAsync(
        Item item, StockMovement movement, CancellationToken ct)
    {
        if (!AffectsPool(movement.MovementType) || !ConsumesLayers(item.CostingType))
        {
            // Transfers move nothing to cost, and weighted average keeps a
            // running figure rather than layers. Inbound still needs its layer,
            // so only outbound is genuinely nothing to do.
            if (movement.Direction == StockDirection.Out || !AffectsPool(movement.MovementType))
            {
                return StockOutcome.Ok;
            }
        }

        if (movement.Direction == StockDirection.In)
        {
            return await CostInboundAsync(item, movement, ct);
        }

        List<long> serialLayers = await SerialLayersAsync(item, movement, ct);

        (StockOutcome outcome, decimal cost) =
            await ConsumeAsync(item, movement, serialLayers, ct);

        if (outcome != StockOutcome.Ok)
        {
            return outcome;
        }

        await StampCostAsync(movement, cost, ct);
        return StockOutcome.Ok;
    }

    /// <summary>
    /// A receipt becomes a layer; a return goes back onto the layers it left
    /// from. Serials received earlier are pointed at the new layer here, because
    /// the layer did not exist when the request created them.
    /// </summary>
    private async Task<StockOutcome> CostInboundAsync(
        Item item, StockMovement movement, CancellationToken ct)
    {
        if (IsReturn(movement.MovementType) && movement.ReturnsStockMovementId is long returnedId)
        {
            (StockOutcome returned, decimal returnedCost) =
                await ReturnToLayersAsync(item, movement, returnedId, ct);

            if (returned != StockOutcome.Ok)
            {
                return returned;
            }

            if (ConsumesLayers(item.CostingType))
            {
                await StampCostAsync(movement, returnedCost, ct);
            }

            await _db.SaveChangesAsync(ct);
            return StockOutcome.Ok;
        }

        // The lot was resolved when the movement was saved, so the expiry that
        // FEFO orders by comes straight off it.
        DateOnly? expiresOn = movement.ItemBatchId is null
            ? null
            : await _db.ItemBatches
                .Where(b => b.ItemBatchId == movement.ItemBatchId)
                .Select(b => b.ExpiryDate)
                .FirstOrDefaultAsync(ct);

        CostLayer layer = CreateLayer(
            item, movement, movement.ItemBatchId, expiresOn, movement.UnitCost ?? 0m);

        await _db.SaveChangesAsync(ct);

        // Serials were created with the movement, before the layer existed.
        await _db.ItemSerials
            .Where(s => s.ReceivedMovementId == movement.StockMovementId && s.CostLayerId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CostLayerId, layer.CostLayerId), ct);

        // A receipt dated before sales that already happened means those sales
        // were costed against stock that should have gone out first. They go
        // back in the queue rather than being replayed here, so the worker keeps
        // processing one movement at a time in order.
        if (await IsBackdatedAsync(item, movement, ct))
        {
            await RequeueAffectedAsync(item, movement, ct);
        }

        return StockOutcome.Ok;
    }

    /// <summary>
    /// Puts every issue on or after a backdated receipt back in the queue, with
    /// its current allocations unwound. The worker then recosts them in date
    /// order like any other pending work — which is what keeps recosting from
    /// needing a second, different code path.
    /// </summary>
    private async Task RequeueAffectedAsync(
        Item item, StockMovement trigger, CancellationToken ct)
    {
        List<StockMovement> affected = await _db.StockMovements
            .Where(m => m.ItemId == item.ItemId
                && m.Direction == StockDirection.Out
                && m.MovementDate >= trigger.MovementDate
                && m.StockMovementId != trigger.StockMovementId
                && m.CostingStatus == CostingStatus.Costed)
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.StockMovementId)
            .ToListAsync(ct);

        if (affected.Count == 0)
        {
            return;
        }

        Guid batch = Guid.NewGuid();
        DateTimeOffset runAt = DateTimeOffset.UtcNow;

        Dictionary<long, decimal> previous = await UnwindAsync(affected, batch, runAt, ct);

        foreach (StockMovement issue in affected)
        {
            issue.CostingStatus = CostingStatus.Pending;
            issue.CostedAt = null;

            // Back into the ledger queue as well. The sale's cost of goods sold
            // is about to change, so the rows already posted for it are about to
            // be wrong. Posting the same key again replaces them, which is why
            // this is a status reset rather than a correcting entry to compose
            // here — the restatement is recorded in its own right below.
            issue.LedgerStatus = LedgerStatus.Pending;
            issue.LedgerPostedAt = null;
            issue.LedgerAttempts = 0;

            // Held so the replay can report what the cost was before it ran.
            _db.RecostingAdjustments.Add(new RecostingAdjustment
            {
                RecostingBatchId = batch,
                ItemId = item.ItemId,
                StockMovementId = issue.StockMovementId,
                TriggerStockMovementId = trigger.StockMovementId,
                PreviousCost = previous.GetValueOrDefault(issue.StockMovementId),
                NewCost = 0m,
                Delta = -previous.GetValueOrDefault(issue.StockMovementId),
                RunAt = runAt,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Writes the settled cost onto the movement, and completes any recosting
    /// adjustment waiting on it.
    /// </summary>
    private async Task StampCostAsync(
        StockMovement movement, decimal cost, CancellationToken ct)
    {
        await _db.StockMovements
            .Where(m => m.StockMovementId == movement.StockMovementId)
            .ExecuteUpdateAsync(
                m => m
                    .SetProperty(x => x.TotalCost, cost)
                    .SetProperty(
                        x => x.UnitCost,
                        movement.Quantity > 0
                            ? Math.Round(cost / movement.Quantity, 6, MidpointRounding.AwayFromZero)
                            : null),
                ct);

        // An adjustment written by RequeueAffectedAsync is only half finished —
        // it knows what the cost was, not what it became. Close it here.
        await _db.RecostingAdjustments
            .Where(a => a.StockMovementId == movement.StockMovementId && a.NewCost == 0m)
            .ExecuteUpdateAsync(
                a => a
                    .SetProperty(x => x.NewCost, cost)
                    .SetProperty(x => x.Delta, x => cost - x.PreviousCost),
                ct);
    }

    private static bool IsReturn(StockMovementType type) =>
        type is StockMovementType.SalesReturn or StockMovementType.PurchaseReturn;

    private static bool AffectsPool(StockMovementType type) =>
        type is not (StockMovementType.TransferIn or StockMovementType.TransferOut);

    /// <summary>
    /// Retires the current allocations of the given issues and gives their
    /// quantity back to the layers. Returns what each issue cost before, so the
    /// replay can report a delta against it.
    /// </summary>
    private async Task<Dictionary<long, decimal>> UnwindAsync(
        List<StockMovement> affected, Guid batch, DateTimeOffset runAt, CancellationToken ct)
    {
        long[] ids = affected.Select(m => m.StockMovementId).ToArray();

        List<CostLayerConsumption> current = await _db.CostLayerConsumptions
            .Where(c => ids.Contains(c.StockMovementId) && c.SupersededAt == null)
            .ToListAsync(ct);

        var previous = new Dictionary<long, decimal>();

        foreach (CostLayerConsumption allocation in current)
        {
            previous[allocation.StockMovementId] =
                previous.GetValueOrDefault(allocation.StockMovementId) + allocation.TotalCost;

            decimal give = allocation.Quantity;

            await _db.CostLayers
                .Where(l => l.CostLayerId == allocation.CostLayerId
                    && l.RemainingQuantity + give <= l.OriginalQuantity)
                .ExecuteUpdateAsync(
                    l => l.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity + give),
                    ct);

            allocation.SupersededAt = runAt;
            allocation.RecostingBatchId = batch;
        }

        await _db.SaveChangesAsync(ct);
        return previous;
    }

    /// <summary>
    /// The layers the pieces on this issue came in on. Specific identification
    /// has no selection rule to replay — the same pieces went out, so the same
    /// layers are consumed, and only their costs can have changed.
    /// </summary>
    private async Task<List<long>> SerialLayersAsync(
        Item item, StockMovement issue, CancellationToken ct)
    {
        if (item.CostingType != CostingType.SpecificIdentification)
        {
            return [];
        }

        return await _db.ItemSerials
            .Where(s => s.IssuedMovementId == issue.StockMovementId && s.CostLayerId != null)
            .Select(s => s.CostLayerId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>Whether this receipt lands before issues that have already been costed.</summary>
    public Task<bool> IsBackdatedAsync(Item item, StockMovement receipt, CancellationToken ct) =>
        !ConsumesLayers(item.CostingType)
            ? Task.FromResult(false)
            : _db.StockMovements.AnyAsync(
                m => m.ItemId == item.ItemId
                    && m.Direction == StockDirection.Out
                    && m.MovementDate >= receipt.MovementDate
                    && m.StockMovementId != receipt.StockMovementId,
                ct);

    /// <summary>
    /// Puts returned stock back on the layers it left from, at the cost it left
    /// at. Without this a return re-enters at whatever the running average
    /// happens to be now, and buy-sell-return quietly changes what the stock is
    /// worth even though nothing was bought or sold on net.
    ///
    /// Allocations are read from the original issue and given back in
    /// proportion, oldest first, so a partial return takes back part of each.
    /// The rows written are ordinary consumption rows against the return
    /// movement — the movement's own direction is what says they are coming back
    /// rather than going out, which keeps the table append-only.
    /// </summary>
    public async Task<(StockOutcome Outcome, decimal TotalCost)> ReturnToLayersAsync(
        Item item,
        StockMovement returnMovement,
        long returnedMovementId,
        CancellationToken ct)
    {
        if (!ConsumesLayers(item.CostingType))
        {
            return (StockOutcome.Ok, 0m);
        }

        List<CostLayerConsumption> original = await _db.CostLayerConsumptions
            .AsNoTracking()
            .Where(c => c.StockMovementId == returnedMovementId && c.SupersededAt == null)
            .OrderBy(c => c.CostLayerConsumptionId)
            .ToListAsync(ct);

        if (original.Count == 0)
        {
            return (StockOutcome.ReturnedMovementNotFound, 0m);
        }

        // What has already come back on earlier partial returns, so two returns
        // of the same issue cannot together give back more than went out.
        decimal alreadyReturned = await _db.CostLayerConsumptions
            .Where(c => c.SupersededAt == null)
            .Where(c => _db.StockMovements
                .Any(m => m.StockMovementId == c.StockMovementId
                    && m.ReturnsStockMovementId == returnedMovementId))
            .SumAsync(c => (decimal?)c.Quantity, ct) ?? 0m;

        decimal issued = original.Sum(c => c.Quantity);

        if (returnMovement.Quantity + alreadyReturned > issued)
        {
            return (StockOutcome.ReturnExceedsIssue, 0m);
        }

        decimal outstanding = returnMovement.Quantity;
        decimal total = 0m;

        foreach (CostLayerConsumption allocation in original)
        {
            if (outstanding <= 0)
            {
                break;
            }

            decimal give = Math.Min(outstanding, allocation.Quantity);

            // Guarded by the layer's own ceiling: a layer can never hold more
            // than it originally received, so an over-return is refused by the
            // database rather than silently inflating stock.
            int restored = await _db.CostLayers
                .Where(l => l.CostLayerId == allocation.CostLayerId
                    && l.RemainingQuantity + give <= l.OriginalQuantity)
                .ExecuteUpdateAsync(
                    l => l.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity + give),
                    ct);

            if (restored == 0)
            {
                continue;
            }

            decimal cost = Math.Round(
                give * allocation.UnitCost, 2, MidpointRounding.AwayFromZero);

            _db.CostLayerConsumptions.Add(new CostLayerConsumption
            {
                CostLayerId = allocation.CostLayerId,
                StockMovementId = returnMovement.StockMovementId,
                Quantity = give,
                UnitCost = allocation.UnitCost,
                TotalCost = cost,
            });

            outstanding = Math.Round(
                outstanding - give, QuantityScale, MidpointRounding.AwayFromZero);
            total += cost;
        }

        return outstanding > 0
            ? (StockOutcome.InsufficientCostLayers, 0m)
            : (StockOutcome.Ok, total);
    }

    /// <summary>Value straight from the layers — what a layered item is actually worth.</summary>
    public async Task<decimal> LayeredValueAsync(long itemId, CancellationToken ct) =>
        await _db.CostLayers
            .Where(l => l.ItemId == itemId && l.RemainingQuantity > 0)
            .SumAsync(l => (decimal?)(l.RemainingQuantity * l.UnitCost), ct) ?? 0m;

    /// <summary>What an issue actually took, for the stock ledger and a margin query.</summary>
    public async Task<IReadOnlyList<CostAllocationItem>> AllocationsAsync(
        long stockMovementId, CancellationToken ct) =>
        await _db.CostLayerConsumptions
            .Where(c => c.StockMovementId == stockMovementId && c.SupersededAt == null)
            .Join(_db.CostLayers, c => c.CostLayerId, l => l.CostLayerId, (c, l) => new { c, l })
            .OrderBy(x => x.l.ReceivedOn)
            .Select(x => new CostAllocationItem
            {
                CostLayerId = x.c.CostLayerId,
                StockMovementId = x.c.StockMovementId,
                ReceivedOn = x.l.ReceivedOn,
                ExpiresOn = x.l.ExpiresOn,
                BatchNumber = _db.ItemBatches
                    .Where(batch => batch.ItemBatchId == x.l.ItemBatchId)
                    .Select(batch => batch.BatchNumber)
                    .FirstOrDefault(),
                Quantity = x.c.Quantity,
                UnitCost = x.c.UnitCost,
                TotalCost = x.c.TotalCost,
            })
            .ToListAsync(ct);

    /// <summary>
    /// The layers this issue may take from, in the order the item's method says
    /// to take them.
    /// </summary>
    private async Task<List<CostLayer>> CandidatesAsync(
        Item item, IReadOnlyList<long> serialLayerIds, CancellationToken ct)
    {
        // Read fresh each pass: ExecuteUpdate above changed rows in the database
        // and not in the change tracker, so a cached instance would report a
        // remaining quantity that has already been spent.
        IQueryable<CostLayer> query = _db.CostLayers
            .AsNoTracking()
            .Where(l => l.ItemId == item.ItemId && l.RemainingQuantity > 0);

        if (item.CostingType == CostingType.SpecificIdentification)
        {
            // No selection rule: the pieces named on the issue decide, and each
            // one already knows the layer it arrived on.
            return await query
                .Where(l => serialLayerIds.Contains(l.CostLayerId))
                .ToListAsync(ct);
        }

        query = item.CostingType switch
        {
            CostingType.Fifo => query
                .OrderBy(l => l.ReceivedOn)
                .ThenBy(l => l.CostLayerId),

            CostingType.Lifo => query
                .OrderByDescending(l => l.ReceivedOn)
                .ThenByDescending(l => l.CostLayerId),

            // Nulls last, deliberately: something that never expires should go
            // out after something that does.
            CostingType.Fefo => query
                .OrderBy(l => l.ExpiresOn == null)
                .ThenBy(l => l.ExpiresOn)
                .ThenBy(l => l.ReceivedOn)
                .ThenBy(l => l.CostLayerId),

            _ => query.OrderBy(l => l.ReceivedOn).ThenBy(l => l.CostLayerId),
        };

        return await query.ToListAsync(ct);
    }

    /// <summary>
    /// Weighted average keeps no layers to draw down — its cost is the running
    /// average on the stock row, and consuming layers would be arithmetic nobody
    /// reads. None is a service, which has no stock at all.
    /// </summary>
    public static bool ConsumesLayers(CostingType costing) =>
        costing is CostingType.Fifo
            or CostingType.Lifo
            or CostingType.Fefo
            or CostingType.SpecificIdentification;
}

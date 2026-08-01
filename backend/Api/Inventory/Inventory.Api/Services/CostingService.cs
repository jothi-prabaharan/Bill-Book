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
        CancellationToken ct)
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

    /// <summary>What an issue actually took, for the stock ledger and a margin query.</summary>
    public async Task<IReadOnlyList<CostAllocationItem>> AllocationsAsync(
        long stockMovementId, CancellationToken ct) =>
        await _db.CostLayerConsumptions
            .Where(c => c.StockMovementId == stockMovementId)
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

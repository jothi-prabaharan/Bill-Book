using Inventory.Entity.Enums;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Services;

/// <summary>What one item's recalculation changed.</summary>
/// <param name="ItemId">The item.</param>
/// <param name="StockOutsRevalued">Stock-outs whose stored cost was rewritten.</param>
/// <param name="StockOutsRequeuedForPosting">
/// Of those, the ones whose line value changed and so owe the ledger a new posting.
/// </param>
/// <param name="AverageCost">The item's average after its last movement.</param>
public sealed record WeightedAverageRecostResult(
    long ItemId,
    int StockOutsRevalued,
    int StockOutsRequeuedForPosting,
    decimal AverageCost);

/// <summary>
/// Applies <see cref="WeightedAverageCalculator"/> to one weighted-average item:
/// reads its movements, recalculates, and writes back only what changed.
///
/// <b>Why a recalculation over the whole history rather than a running
/// update.</b> The request path keeps the average in the order movements
/// <i>arrive</i>, because it has to answer a till immediately. That order is
/// wrong as soon as anything is backdated: a receipt keyed today for last week
/// should have changed the cost of every sale since last week, and a sale keyed
/// before its purchase was valued at whatever the average happened to be. Only
/// a pass in date order over everything can put those right, and it is cheap
/// enough — one projection of six columns per movement — to run whenever the
/// item has new work.
///
/// <b>What it writes.</b> A stock-out's unit cost, line value and resulting
/// average; a stock-in's resulting average, which is an audit figure and never
/// posted; and the item's average on <c>inv.ItemStock</c>. A stock-out whose
/// <i>line value</i> changed goes back in the ledger queue, and only those —
/// posting the same key again replaces the rows already there, so the ledger
/// is corrected line by line rather than by an entry composed here.
///
/// <b>What it deliberately does not write: the quantity on hand.</b> That
/// figure belongs to the guarded decrement in the request path, which is what
/// stops two tills selling the last unit. Overwriting it from here would race
/// that decrement and could restore a unit already sold. A disagreement is
/// logged instead, because it means the two have drifted and a person should
/// look.
///
/// Runs inside the caller's transaction; it opens none of its own.
/// </summary>
public sealed class WeightedAverageRecosting
{
    private readonly InventoryDbContext _db;
    private readonly ILogger<WeightedAverageRecosting> _log;

    public WeightedAverageRecosting(InventoryDbContext db, ILogger<WeightedAverageRecosting> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<WeightedAverageRecostResult> RecalculateAsync(
        long itemId, DateOnly? lockDate, CancellationToken ct)
    {
        // Every movement that moves the pool. A transfer changes where stock
        // sits and not how much there is, so it has no place in the average.
        var stored = await _db.StockMovements
            .AsNoTracking()
            .Where(m => m.ItemId == itemId
                && m.MovementType != StockMovementType.TransferIn
                && m.MovementType != StockMovementType.TransferOut
                && m.Quantity > 0)
            .Select(m => new
            {
                m.StockMovementId,
                m.MovementDate,
                m.Direction,
                m.Quantity,
                m.UnitCost,
                m.TotalCost,
                m.ResultingWeightedAverageCost,
                m.LedgerStatus,
                m.LedgerExempt,
            })
            .ToListAsync(ct);

        WeightedAverageResult result = WeightedAverageCalculator.Calculate(
            stored.Select(m => new WeightedAverageLine(
                m.StockMovementId,
                m.MovementDate,
                m.Direction,
                m.Quantity,
                m.UnitCost,
                m.TotalCost)),
            lockDate);

        var byId = stored.ToDictionary(m => m.StockMovementId);

        int revalued = 0;
        int requeued = 0;

        foreach (WeightedAverageLineResult line in result.Lines)
        {
            var before = byId[line.StockMovementId];

            if (line.Direction == StockDirection.In)
            {
                // A stock-in keeps its own cost. Only the audit figure beside
                // it — what the average became — can be out of date, and never
                // on or before the lock date, which is not touched at all.
                bool lockedIn = lockDate is DateOnly limit && before.MovementDate <= limit;

                if (!lockedIn && before.ResultingWeightedAverageCost != line.ResultingAverage)
                {
                    await _db.StockMovements
                        .Where(m => m.StockMovementId == line.StockMovementId)
                        .ExecuteUpdateAsync(
                            m => m.SetProperty(
                                x => x.ResultingWeightedAverageCost, line.ResultingAverage),
                            ct);
                }

                continue;
            }

            if (!line.Recalculated)
            {
                continue;
            }

            bool valueChanged = before.TotalCost != line.TotalCost;
            bool costChanged = before.UnitCost != line.UnitCost
                || before.ResultingWeightedAverageCost != line.ResultingAverage;

            if (!valueChanged && !costChanged)
            {
                continue;
            }

            revalued++;

            // A line whose value is unchanged owes the ledger nothing, even if
            // its unit cost moved in the twelfth decimal. A line still waiting
            // to be posted will post the new figure when it gets there, so it
            // is left where it is.
            // An exempt movement (a job-work or sample challan, TK-90) is never
            // requeued: it owes the ledger nothing, whatever it now costs.
            bool repost = valueChanged
                && before.LedgerStatus != LedgerStatus.Pending
                && !before.LedgerExempt;

            if (repost)
            {
                requeued++;

                await _db.StockMovements
                    .Where(m => m.StockMovementId == line.StockMovementId)
                    .ExecuteUpdateAsync(
                        m => m
                            .SetProperty(x => x.UnitCost, line.UnitCost)
                            .SetProperty(x => x.TotalCost, line.TotalCost)
                            .SetProperty(x => x.ResultingWeightedAverageCost, line.ResultingAverage)
                            .SetProperty(x => x.LedgerStatus, LedgerStatus.Pending)
                            .SetProperty(x => x.LedgerPostedAt, (DateTimeOffset?)null)
                            .SetProperty(x => x.LedgerAttempts, 0)
                            .SetProperty(x => x.LedgerError, (string?)null),
                        ct);
            }
            else
            {
                await _db.StockMovements
                    .Where(m => m.StockMovementId == line.StockMovementId)
                    .ExecuteUpdateAsync(
                        m => m
                            .SetProperty(x => x.UnitCost, line.UnitCost)
                            .SetProperty(x => x.TotalCost, line.TotalCost)
                            .SetProperty(x => x.ResultingWeightedAverageCost, line.ResultingAverage),
                        ct);
            }
        }

        await RefreshPositionAsync(itemId, result, ct);

        return new WeightedAverageRecostResult(itemId, revalued, requeued, result.AverageCost);
    }

    /// <summary>
    /// Sets the item's average to where the recalculation ended. The quantity is
    /// compared and never written — see the class summary for why.
    /// </summary>
    private async Task RefreshPositionAsync(
        long itemId, WeightedAverageResult result, CancellationToken ct)
    {
        decimal average = result.AverageCost;

        await _db.ItemStock
            .Where(s => s.ItemId == itemId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.WeightedAverageCost, average), ct);

        decimal? onHand = await _db.ItemStock
            .AsNoTracking()
            .Where(s => s.ItemId == itemId)
            .Select(s => (decimal?)s.QuantityOnHand)
            .FirstOrDefaultAsync(ct);

        if (onHand is decimal held && held != result.Quantity)
        {
            _log.LogWarning(
                "Item {ItemId} holds {OnHand} on hand but its movements add up to {Quantity}. "
                    + "The average was recalculated from the movements; the quantity was left alone.",
                itemId,
                held,
                result.Quantity);
        }
    }
}

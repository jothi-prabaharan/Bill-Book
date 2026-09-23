using Inventory.Entity.Enums;

namespace Inventory.Api.Services;

/// <summary>
/// One movement as the weighted average recalculation sees it: a date, a
/// direction, a quantity, and whatever cost is already stored on the row.
/// </summary>
/// <param name="StockMovementId">Entry order, and the last tie-breaker.</param>
/// <param name="MovementDate">The date the stock moved.</param>
/// <param name="Direction">In or out. The quantity is never signed.</param>
/// <param name="Quantity">In the item's inventory unit, always positive.</param>
/// <param name="UnitCost">
/// On a stock-in, its own cost — which is never recalculated. On a stock-out,
/// whatever was last stored.
/// </param>
/// <param name="TotalCost">
/// On a stock-out, the line value last stored. Read for a stock-out that is not
/// recalculated, so it still counts toward the running value.
/// </param>
public sealed record WeightedAverageLine(
    long StockMovementId,
    DateOnly MovementDate,
    StockDirection Direction,
    decimal Quantity,
    decimal? UnitCost,
    decimal? TotalCost);

/// <summary>
/// One movement after the recalculation, in the order it was processed.
/// </summary>
/// <param name="StockMovementId">The movement.</param>
/// <param name="Direction">In or out.</param>
/// <param name="Recalculated">
/// True only for a stock-out whose value this run decided. False for every
/// stock-in, for a stock-out on or before the lock date, and for a stock-out
/// with no stock-in anywhere before it to take an average from.
/// </param>
/// <param name="UnitCost">
/// The average applied to a recalculated stock-out, or a stock-in's own cost.
/// Otherwise what was stored.
/// </param>
/// <param name="UnroundedValue"><c>Quantity × UnitCost</c> to 12 decimals.</param>
/// <param name="TotalCost">
/// The 2-decimal line value — the amount posted. On a recalculated stock-out it
/// carries the cumulative cent correction, so it can sit 0.01 away from its own
/// <c>UnroundedValue</c> rounded.
/// </param>
/// <param name="ResultingAverage">The average in force after this movement, or null before the first stock-in.</param>
/// <param name="RunningQuantity">Quantity after this movement.</param>
/// <param name="RunningValue">Value after this movement, from unrounded line values.</param>
public sealed record WeightedAverageLineResult(
    long StockMovementId,
    StockDirection Direction,
    bool Recalculated,
    decimal? UnitCost,
    decimal? UnroundedValue,
    decimal? TotalCost,
    decimal? ResultingAverage,
    decimal RunningQuantity,
    decimal RunningValue);

/// <summary>What a recalculation produced, and where the item ended up.</summary>
/// <param name="Lines">Every movement, in processing order.</param>
/// <param name="Quantity">The final running quantity.</param>
/// <param name="Value">The final running value.</param>
/// <param name="AverageCost">
/// The item's average after the last movement: value ÷ quantity while both are
/// positive, otherwise the last average a stock-in set, otherwise zero.
/// </param>
public sealed record WeightedAverageResult(
    IReadOnlyList<WeightedAverageLineResult> Lines,
    decimal Quantity,
    decimal Value,
    decimal AverageCost);

/// <summary>
/// The moving weighted average, recalculated over an item's whole history.
///
/// <b>Pure.</b> No database and no clock: a list of movements and a lock date
/// in, the value of every stock-out back. That is what lets the owner's worked
/// example be asserted to the cent without a server, and it is the part that
/// fails quietly — a wrong average produces a stock valuation that still adds
/// up and a gross margin that is simply untrue.
///
/// The rules, in the order they apply:
///
/// <list type="number">
/// <item><b>Order.</b> By movement date; on the same date every stock-in before
/// every stock-out; then by entry order.</item>
/// <item><b>Negative stock.</b> A stock-out that would take the running quantity
/// below zero has the next stock-in after it moved ahead of it, repeatedly,
/// until the quantity holds or there is no later stock-in left. A sale keyed
/// before its purchase is then valued at the cost of the stock that actually
/// covered it, rather than at nothing.</item>
/// <item><b>Averaging.</b> At each stock-in the average becomes running value ÷
/// running quantity, and every stock-out up to the next stock-in is valued at
/// it. A stock-in keeps its own cost; only stock-outs are revalued.</item>
/// <item><b>Lock date.</b> A stock-out dated on or before it is never changed,
/// but its stored value still counts toward the running value, so it still
/// shapes every average after it.</item>
/// <item><b>Rounding.</b> The average and the unrounded line value are held to
/// 12 decimals; the line value posted is 2. After each recalculated stock-out
/// the posted values so far must equal the unrounded running total rounded to 2
/// decimals, and the line where they first part takes the difference — so the
/// item's total cost of sales is exact to the cent.</item>
/// </list>
/// </summary>
public static class WeightedAverageCalculator
{
    /// <summary>Precision of the average, the unit cost and the unrounded line value.</summary>
    public const int CostScale = 12;

    /// <summary>Precision of the line value that is stored and posted.</summary>
    public const int ValueScale = 2;

    public static WeightedAverageResult Calculate(
        IEnumerable<WeightedAverageLine> movements, DateOnly? lockDate)
    {
        List<WeightedAverageLine> ordered = Order(movements);
        CoverNegativeStock(ordered);

        var results = new List<WeightedAverageLineResult>(ordered.Count);

        decimal quantity = 0m;
        decimal value = 0m;
        decimal? average = null;

        // The cumulative cent correction runs across every stock-out this run
        // values, not per stock-in segment: it is the item's total cost of sales
        // that has to be exact, and a correction reset at each receipt would let
        // the drift accumulate again across segments.
        decimal unroundedTotal = 0m;
        decimal postedTotal = 0m;

        foreach (WeightedAverageLine line in ordered)
        {
            if (line.Direction == StockDirection.In)
            {
                decimal ownCost = line.UnitCost ?? 0m;

                // Quantity × its own cost, not the rounded line value: the
                // worked example's 6 × 11.3333 adds 67.9998, not 68.00.
                decimal added = line.Quantity * ownCost;

                quantity += line.Quantity;
                value += added;

                // A stock-in that lands while the running quantity is still at
                // or below zero has nothing to average against, and dividing by
                // it would produce a number with no meaning. Its own cost is the
                // only honest answer.
                average = Cost(quantity > 0 ? value / quantity : ownCost);

                results.Add(new WeightedAverageLineResult(
                    line.StockMovementId,
                    line.Direction,
                    Recalculated: false,
                    line.UnitCost,
                    Cost(added),
                    line.TotalCost,
                    average,
                    quantity,
                    value));

                continue;
            }

            bool locked = lockDate is DateOnly limit && line.MovementDate <= limit;

            if (locked || average is not decimal current)
            {
                // Left exactly as stored. Locked, because the period is closed;
                // or with no average yet, because no stock-in exists anywhere
                // before it — the negative-stock rule would have brought one
                // forward if there were a later one to bring.
                decimal stored = line.TotalCost ?? 0m;

                quantity -= line.Quantity;
                value -= stored;

                results.Add(new WeightedAverageLineResult(
                    line.StockMovementId,
                    line.Direction,
                    Recalculated: false,
                    line.UnitCost,
                    UnroundedValue: null,
                    line.TotalCost,
                    average,
                    quantity,
                    value));

                continue;
            }

            decimal unrounded = Cost(line.Quantity * current);

            unroundedTotal += unrounded;

            // What the posted lines should add up to by now, less what they
            // already do. Equal to this line rounded on its own unless the
            // running total has drifted a cent, in which case this is the line
            // that absorbs it. Never negative: the rounded running total only
            // grows, and the posted total is the previous rounded running total.
            decimal posted = Value(unroundedTotal) - postedTotal;
            postedTotal += posted;

            quantity -= line.Quantity;

            // The running value moves by the unrounded figure, which is what
            // the worked example's running-value column shows (30.8571, not
            // 30.86).
            value -= unrounded;

            results.Add(new WeightedAverageLineResult(
                line.StockMovementId,
                line.Direction,
                Recalculated: true,
                current,
                unrounded,
                posted,
                current,
                quantity,
                value));
        }

        decimal finalAverage = quantity > 0 && value >= 0
            ? Cost(value / quantity)
            : average ?? 0m;

        return new WeightedAverageResult(results, quantity, value, finalAverage);
    }

    /// <summary>
    /// Date, then stock-in before stock-out on the same date, then entry order.
    /// The direction is compared explicitly rather than through the enum's
    /// numeric value, so reordering the enum could not silently change this.
    /// </summary>
    public static List<WeightedAverageLine> Order(IEnumerable<WeightedAverageLine> movements) =>
        [.. movements
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.Direction == StockDirection.In ? 0 : 1)
            .ThenBy(m => m.StockMovementId)];

    /// <summary>
    /// Brings the next stock-in forward ahead of any stock-out that would take
    /// the running quantity below zero. In place.
    ///
    /// Terminates because every move takes a stock-in from later in the list
    /// to earlier, and a stock-in once moved ahead is never moved again: the
    /// search for the next one always starts after the stock-out in question.
    /// </summary>
    public static void CoverNegativeStock(List<WeightedAverageLine> ordered)
    {
        decimal quantity = 0m;
        int index = 0;

        while (index < ordered.Count)
        {
            WeightedAverageLine line = ordered[index];

            if (line.Direction == StockDirection.In)
            {
                quantity += line.Quantity;
                index++;
                continue;
            }

            if (quantity - line.Quantity < 0)
            {
                int next = ordered.FindIndex(
                    index + 1, m => m.Direction == StockDirection.In);

                if (next >= 0)
                {
                    // The stock-in takes this stock-out's place, and the loop
                    // looks at the same position again — now the stock-in — so
                    // the stock-out is re-tested against the larger quantity.
                    WeightedAverageLine covering = ordered[next];
                    ordered.RemoveAt(next);
                    ordered.Insert(index, covering);
                    continue;
                }
            }

            // Covered, or nothing left to cover it with. Either way it goes out.
            quantity -= line.Quantity;
            index++;
        }
    }

    private static decimal Cost(decimal amount) =>
        Math.Round(amount, CostScale, MidpointRounding.AwayFromZero);

    private static decimal Value(decimal amount) =>
        Math.Round(amount, ValueScale, MidpointRounding.AwayFromZero);
}

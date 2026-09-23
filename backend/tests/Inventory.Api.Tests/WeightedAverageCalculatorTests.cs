using Inventory.Api.Services;
using Inventory.Entity.Enums;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// The weighted average recalculation, with no database.
///
/// The calculator is pure, so these assert the owner's specification directly —
/// the worked example to the cent, the negative-stock example, the lock date and
/// the rounding correction — rather than asserting that a mock behaves like a
/// mock. It is the piece that fails quietly: a wrong average leaves a valuation
/// that still adds up and a gross margin that is simply untrue.
/// </summary>
public class WeightedAverageCalculatorTests
{
    private static readonly DateOnly Day1 = new(2026, 9, 1);

    private static WeightedAverageLine In(long id, decimal quantity, decimal unitCost, DateOnly? on = null) =>
        new(id, on ?? Day1, StockDirection.In, quantity, unitCost, Math.Round(quantity * unitCost, 2));

    private static WeightedAverageLine Out(
        long id, decimal quantity, DateOnly? on = null, decimal? storedUnitCost = null, decimal? storedTotal = null) =>
        new(id, on ?? Day1, StockDirection.Out, quantity, storedUnitCost, storedTotal);

    /// <summary>The owner's worked example, row by row.</summary>
    private static List<WeightedAverageLine> WorkedExample() =>
    [
        In(1, 3m, 10m, Day1),
        In(2, 4m, 10.5m, Day1.AddDays(1)),
        Out(3, 2m, Day1.AddDays(2)),
        Out(4, 2m, Day1.AddDays(3)),
        In(5, 6m, 11.3333m, Day1.AddDays(4)),
        Out(6, 1m, Day1.AddDays(5)),
        Out(7, 3m, Day1.AddDays(6)),
    ];

    [Fact]
    public void The_worked_example_values_every_stock_out_to_the_cent()
    {
        WeightedAverageResult result = WeightedAverageCalculator.Calculate(WorkedExample(), null);

        var outs = result.Lines.Where(l => l.Direction == StockDirection.Out).ToList();

        Assert.Equal([3L, 4L, 6L, 7L], outs.Select(l => l.StockMovementId));
        Assert.Equal([20.57m, 20.57m, 10.99m, 32.95m], outs.Select(l => l.TotalCost!.Value));
        Assert.All(outs, l => Assert.True(l.Recalculated));

        // The total is exact: 85.08, which is the unrounded 85.0792… rounded.
        Assert.Equal(85.08m, outs.Sum(l => l.TotalCost!.Value));
    }

    [Fact]
    public void The_average_is_taken_at_each_stock_in_and_held_to_twelve_decimals()
    {
        WeightedAverageResult result = WeightedAverageCalculator.Calculate(WorkedExample(), null);

        WeightedAverageLineResult Line(long id) => result.Lines.Single(l => l.StockMovementId == id);

        // 72 ÷ 7, used by rows 3 and 4.
        Assert.Equal(10.285714285714m, Line(2).ResultingAverage);
        Assert.Equal(10.285714285714m, Line(3).UnitCost);
        Assert.Equal(10.285714285714m, Line(4).UnitCost);

        // 98.8569… ÷ 9, used by rows 6 and 7.
        Assert.Equal(10.984104761905m, Line(5).ResultingAverage);
        Assert.Equal(10.984104761905m, Line(6).UnitCost);
        Assert.Equal(10.984104761905m, Line(7).UnitCost);

        // The unrounded line value is quantity × average to twelve decimals.
        Assert.Equal(20.571428571428m, Line(3).UnroundedValue);
        Assert.Equal(32.952314285715m, Line(7).UnroundedValue);
    }

    [Fact]
    public void A_stock_in_adds_quantity_times_its_own_cost_not_its_rounded_line_value()
    {
        WeightedAverageResult result = WeightedAverageCalculator.Calculate(WorkedExample(), null);

        WeightedAverageLineResult row4 = result.Lines.Single(l => l.StockMovementId == 4);
        WeightedAverageLineResult row5 = result.Lines.Single(l => l.StockMovementId == 5);

        // 6 × 11.3333 = 67.9998 — not the 68.00 its line value rounds to.
        Assert.Equal(67.9998m, row5.RunningValue - row4.RunningValue);

        // A stock-in keeps its own cost; nothing about it is revalued.
        Assert.False(row5.Recalculated);
        Assert.Equal(11.3333m, row5.UnitCost);
    }

    [Fact]
    public void The_running_value_moves_by_the_unrounded_line_value()
    {
        WeightedAverageResult result = WeightedAverageCalculator.Calculate(WorkedExample(), null);

        // 72 − 2 × 20.571428571428: the example's 30.8571, not 72 − 41.14.
        Assert.Equal(
            30.857142857144m,
            result.Lines.Single(l => l.StockMovementId == 4).RunningValue);

        Assert.Equal(5m, result.Quantity);
        Assert.Equal(54.920523809524m, result.Value);
        Assert.Equal(10.984104761905m, result.AverageCost);
    }

    [Fact]
    public void The_cumulative_correction_moves_the_line_where_the_drift_first_appears()
    {
        WeightedAverageResult result = WeightedAverageCalculator.Calculate(WorkedExample(), null);

        WeightedAverageLineResult row6 = result.Lines.Single(l => l.StockMovementId == 6);

        // On its own, 1 × 10.984104761905 rounds to 10.98. The posted lines
        // before it total 41.14 against an unrounded 41.142857…, and after it
        // 52.126961… rounds to 52.13 — so this line takes the cent.
        Assert.Equal(10.98m, Math.Round(row6.UnroundedValue!.Value, 2, MidpointRounding.AwayFromZero));
        Assert.Equal(10.99m, row6.TotalCost);
    }

    [Fact]
    public void Posted_values_always_add_up_to_the_unrounded_total_rounded()
    {
        // Thirds, which drift on every line: 10 ÷ 3 = 3.333… per unit.
        List<WeightedAverageLine> lines = [In(1, 3m, 10m / 3m)];
        for (long id = 2; id <= 12; id++)
        {
            lines.Add(Out(id, 0.2m, Day1.AddDays((int)id)));
        }

        WeightedAverageResult result = WeightedAverageCalculator.Calculate(lines, null);

        var outs = result.Lines.Where(l => l.Recalculated).ToList();
        decimal unrounded = outs.Sum(l => l.UnroundedValue!.Value);

        Assert.Equal(
            Math.Round(unrounded, 2, MidpointRounding.AwayFromZero),
            outs.Sum(l => l.TotalCost!.Value));

        // And no line is ever pushed negative by the correction.
        Assert.All(outs, l => Assert.True(l.TotalCost >= 0m));

        // Nor more than a cent from its own rounded value.
        Assert.All(outs, l => Assert.True(Math.Abs(
            l.TotalCost!.Value - Math.Round(l.UnroundedValue!.Value, 2, MidpointRounding.AwayFromZero))
                <= 0.01m));
    }

    [Fact]
    public void A_stock_out_entered_before_its_stock_arrived_is_valued_at_the_stock_that_covers_it()
    {
        // The owner's negative-stock example: an Out of 5 dated before an In of 8
        // at 9.00. Valued at nothing, the sale would have no cost of goods.
        List<WeightedAverageLine> lines =
        [
            Out(1, 5m, Day1),
            In(2, 8m, 9m, Day1.AddDays(3)),
        ];

        WeightedAverageResult result = WeightedAverageCalculator.Calculate(lines, null);

        Assert.Equal([2L, 1L], result.Lines.Select(l => l.StockMovementId));

        WeightedAverageLineResult sale = result.Lines.Single(l => l.StockMovementId == 1);
        Assert.True(sale.Recalculated);
        Assert.Equal(9m, sale.UnitCost);
        Assert.Equal(45.00m, sale.TotalCost);

        Assert.Equal(3m, result.Quantity);
        Assert.Equal(9m, result.AverageCost);
    }

    [Fact]
    public void Stock_ins_are_brought_forward_until_the_quantity_holds()
    {
        // Two small receipts after a sale that needs both of them.
        List<WeightedAverageLine> ordered = WeightedAverageCalculator.Order(
        [
            Out(1, 5m, Day1),
            In(2, 2m, 10m, Day1.AddDays(1)),
            Out(3, 1m, Day1.AddDays(2)),
            In(4, 4m, 13m, Day1.AddDays(3)),
        ]);

        WeightedAverageCalculator.CoverNegativeStock(ordered);

        Assert.Equal([2L, 4L, 1L, 3L], ordered.Select(l => l.StockMovementId));
    }

    [Fact]
    public void With_no_later_stock_in_left_the_stock_out_stays_where_it_is()
    {
        List<WeightedAverageLine> ordered = WeightedAverageCalculator.Order(
        [
            In(1, 2m, 10m, Day1),
            Out(2, 5m, Day1.AddDays(1)),
        ]);

        WeightedAverageCalculator.CoverNegativeStock(ordered);

        Assert.Equal([1L, 2L], ordered.Select(l => l.StockMovementId));

        WeightedAverageResult result = WeightedAverageCalculator.Calculate(ordered, null);

        // Still valued at the average it had; the quantity simply goes negative.
        Assert.Equal(50.00m, result.Lines.Single(l => l.StockMovementId == 2).TotalCost);
        Assert.Equal(-3m, result.Quantity);
    }

    [Fact]
    public void On_the_same_date_stock_in_comes_before_stock_out_then_entry_order()
    {
        List<WeightedAverageLine> ordered = WeightedAverageCalculator.Order(
        [
            Out(5, 1m, Day1),
            In(9, 1m, 10m, Day1),
            Out(3, 1m, Day1),
            In(7, 1m, 10m, Day1),
            In(1, 1m, 10m, Day1.AddDays(-1)),
        ]);

        Assert.Equal([1L, 7L, 9L, 3L, 5L], ordered.Select(l => l.StockMovementId));
    }

    [Fact]
    public void A_stock_out_on_or_before_the_lock_date_is_not_changed_but_still_counts()
    {
        // Row 3 was stored at 20.00 and is in a closed period. It keeps that,
        // and the 20.00 — not a recalculated 20.57 — leaves the running value.
        List<WeightedAverageLine> lines = WorkedExample();
        lines[2] = Out(3, 2m, Day1.AddDays(2), storedUnitCost: 10m, storedTotal: 20.00m);

        DateOnly lockDate = Day1.AddDays(2);

        WeightedAverageResult result = WeightedAverageCalculator.Calculate(lines, lockDate);

        WeightedAverageLineResult row3 = result.Lines.Single(l => l.StockMovementId == 3);
        Assert.False(row3.Recalculated);
        Assert.Equal(20.00m, row3.TotalCost);
        Assert.Equal(10m, row3.UnitCost);
        Assert.Equal(52.00m, row3.RunningValue);

        // Row 4 is after the lock and still uses the average set at row 2.
        WeightedAverageLineResult row4 = result.Lines.Single(l => l.StockMovementId == 4);
        Assert.True(row4.Recalculated);
        Assert.Equal(20.57m, row4.TotalCost);

        // Row 5's average is built on the locked 20.00, so it differs from the
        // unlocked example's 10.984104761905: (52 − 20.571428571428 + 67.9998) ÷ 9.
        Assert.Equal(
            Math.Round((52m - 20.571428571428m + 67.9998m) / 9m, 12, MidpointRounding.AwayFromZero),
            result.Lines.Single(l => l.StockMovementId == 5).ResultingAverage);
    }

    [Fact]
    public void The_lock_date_is_inclusive()
    {
        List<WeightedAverageLine> lines =
        [
            In(1, 10m, 10m, Day1),
            Out(2, 1m, Day1.AddDays(1), storedUnitCost: 7m, storedTotal: 7m),
        ];

        WeightedAverageResult onTheDay = WeightedAverageCalculator.Calculate(lines, Day1.AddDays(1));
        WeightedAverageResult dayBefore = WeightedAverageCalculator.Calculate(lines, Day1);

        Assert.False(onTheDay.Lines.Single(l => l.StockMovementId == 2).Recalculated);
        Assert.Equal(7m, onTheDay.Lines.Single(l => l.StockMovementId == 2).TotalCost);

        Assert.True(dayBefore.Lines.Single(l => l.StockMovementId == 2).Recalculated);
        Assert.Equal(10m, dayBefore.Lines.Single(l => l.StockMovementId == 2).TotalCost);
    }

    [Fact]
    public void A_stock_out_with_no_stock_in_anywhere_is_left_as_stored()
    {
        List<WeightedAverageLine> lines = [Out(1, 2m, Day1, storedUnitCost: 4m, storedTotal: 8m)];

        WeightedAverageResult result = WeightedAverageCalculator.Calculate(lines, null);

        WeightedAverageLineResult line = Assert.Single(result.Lines);
        Assert.False(line.Recalculated);
        Assert.Equal(8m, line.TotalCost);
        Assert.Equal(0m, result.AverageCost);
    }

    [Fact]
    public void A_backdated_receipt_changes_the_cost_of_every_sale_after_it()
    {
        // Entered in this order: bought at 10, sold 5, then a receipt at 20
        // keyed later but dated before the sale.
        List<WeightedAverageLine> lines =
        [
            In(1, 10m, 10m, Day1),
            Out(2, 5m, Day1.AddDays(5), storedUnitCost: 10m, storedTotal: 50m),
            In(3, 10m, 20m, Day1.AddDays(2)),
        ];

        WeightedAverageResult result = WeightedAverageCalculator.Calculate(lines, null);

        // (100 + 200) ÷ 20 = 15, so the sale cost 75, not the 50 it was stored at.
        WeightedAverageLineResult sale = result.Lines.Single(l => l.StockMovementId == 2);
        Assert.Equal(15m, sale.UnitCost);
        Assert.Equal(75.00m, sale.TotalCost);
    }
}

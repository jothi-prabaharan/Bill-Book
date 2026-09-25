using Preventive.Api.Services;
using Preventive.Entity.Enums;
using Xunit;

namespace Preventive.Api.Tests;

/// <summary>A plan's due dates (S7, TK-67). Pure, so it needs no database.</summary>
public sealed class RecurrenceTests
{
    [Fact]
    public void A_monthly_plan_keeps_its_day_through_short_months()
    {
        var start = new DateOnly(2026, 1, 31);
        DateOnly feb = Recurrence.NextAfter(start, Frequency.Monthly, 1, start);
        DateOnly mar = Recurrence.NextAfter(start, Frequency.Monthly, 1, feb);

        Assert.Equal(new DateOnly(2026, 2, 28), feb);
        Assert.Equal(new DateOnly(2026, 3, 31), mar);
    }

    [Theory]
    [InlineData(Frequency.Daily, 3, "2026-04-04")]
    [InlineData(Frequency.Weekly, 2, "2026-04-15")]
    [InlineData(Frequency.Quarterly, 1, "2026-07-01")]
    [InlineData(Frequency.HalfYearly, 1, "2026-10-01")]
    [InlineData(Frequency.Yearly, 1, "2027-04-01")]
    public void The_next_date_after_the_start_steps_by_frequency_and_interval(Frequency frequency, int interval, string expected)
    {
        var start = new DateOnly(2026, 4, 1);
        Assert.Equal(DateOnly.Parse(expected), Recurrence.NextAfter(start, frequency, interval, start));
    }

    [Fact]
    public void The_next_date_after_a_day_before_the_start_is_the_start() =>
        Assert.Equal(new DateOnly(2026, 4, 1), Recurrence.NextAfter(new DateOnly(2026, 4, 1), Frequency.Monthly, 1, new DateOnly(2026, 1, 1)));

    [Fact]
    public void A_date_between_occurrences_lands_on_the_next_one() =>
        Assert.Equal(new DateOnly(2027, 1, 1), Recurrence.NextAfter(new DateOnly(2026, 4, 1), Frequency.Quarterly, 1, new DateOnly(2026, 11, 15)));

    [Fact]
    public void A_years_old_daily_plan_steps_quickly_and_exactly() =>
        Assert.Equal(new DateOnly(2026, 9, 26), Recurrence.NextAfter(new DateOnly(2020, 1, 1), Frequency.Daily, 1, new DateOnly(2026, 9, 25)));

    [Theory]
    [InlineData("2026-07-10", 0, null, "2026-07-10", true)]
    [InlineData("2026-07-10", 0, null, "2026-07-09", false)]
    [InlineData("2026-07-10", 3, null, "2026-07-07", true)]
    [InlineData("2026-07-10", 0, "2026-07-01", "2026-07-20", false)]
    public void An_occurrence_is_generated_lead_days_early_and_never_after_the_end(string due, int lead, string? end, string today, bool expected) =>
        Assert.Equal(expected, Recurrence.IsInWindow(DateOnly.Parse(due), lead, end is null ? null : DateOnly.Parse(end), DateOnly.Parse(today)));
}

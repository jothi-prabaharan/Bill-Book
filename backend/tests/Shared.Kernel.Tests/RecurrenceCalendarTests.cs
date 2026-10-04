using Shared.Kernel.Recurring;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>The dates a recurring schedule falls on (TK-139).</summary>
public sealed class RecurrenceCalendarTests
{
    [Fact]
    public void Month_ends_are_counted_from_the_start_so_they_do_not_drift()
    {
        DateOnly start = new(2026, 1, 31);

        Assert.Equal(new DateOnly(2026, 2, 28), RecurrenceCalendar.Occurrence(start, RecurrenceFrequency.Monthly, 1, 1));
        Assert.Equal(new DateOnly(2026, 3, 31), RecurrenceCalendar.Occurrence(start, RecurrenceFrequency.Monthly, 1, 2));
    }

    [Theory]
    [InlineData(RecurrenceFrequency.Weekly, 2, 1, "2026-01-15")]
    [InlineData(RecurrenceFrequency.Quarterly, 1, 2, "2026-07-01")]
    [InlineData(RecurrenceFrequency.HalfYearly, 1, 1, "2026-07-01")]
    [InlineData(RecurrenceFrequency.Yearly, 1, 3, "2029-01-01")]
    public void Each_frequency_steps_by_its_period(RecurrenceFrequency frequency, int every, int index, string expected)
    {
        DateOnly start = new(2026, 1, 1);

        Assert.Equal(DateOnly.Parse(expected), RecurrenceCalendar.Occurrence(start, frequency, every, index));
    }

    [Fact]
    public void An_occurrence_past_the_end_date_is_none()
    {
        Assert.Null(RecurrenceCalendar.Next(
            new DateOnly(2026, 1, 1), RecurrenceFrequency.Monthly, 1, 3, endDate: new DateOnly(2026, 3, 31), maxOccurrences: null));

        Assert.Equal(new DateOnly(2026, 3, 1), RecurrenceCalendar.Next(
            new DateOnly(2026, 1, 1), RecurrenceFrequency.Monthly, 1, 2, endDate: new DateOnly(2026, 3, 1), maxOccurrences: null));
    }

    [Fact]
    public void An_occurrence_past_the_limit_is_none()
    {
        Assert.Null(RecurrenceCalendar.Next(
            new DateOnly(2026, 1, 1), RecurrenceFrequency.Monthly, 1, 12, endDate: null, maxOccurrences: 12));
    }

    [Fact]
    public void Resuming_skips_to_the_first_occurrence_not_before_today()
    {
        int index = RecurrenceCalendar.FirstIndexOnOrAfter(
            new DateOnly(2026, 1, 10), RecurrenceFrequency.Monthly, 1, fromIndex: 1, today: new DateOnly(2026, 5, 1));

        Assert.Equal(4, index);
    }
}

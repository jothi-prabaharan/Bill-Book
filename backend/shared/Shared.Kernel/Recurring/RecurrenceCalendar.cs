namespace Shared.Kernel.Recurring;

/// <summary>How often a recurring document is raised (TK-139).</summary>
public enum RecurrenceFrequency
{
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    HalfYearly = 4,
    Yearly = 5,
}

/// <summary>Where a recurring schedule stands (TK-139).</summary>
public enum RecurringScheduleStatus
{
    Active = 1,
    Paused = 2,
    Ended = 3,
}

/// <summary>
/// The dates a recurring schedule falls on (TK-139). Pure, so Sales and
/// Purchase answer alike and a test needs no database.
///
/// <b>Every occurrence is counted from the start date</b>, never from the one
/// before it. A schedule starting on 31 January falls on 28 February and then on
/// 31 March: stepping month by month from the previous date would leave it on the
/// 28th for the rest of its life.
/// </summary>
public static class RecurrenceCalendar
{
    /// <summary>
    /// The most occurrences one run will raise for one schedule. A worker that
    /// was down for a year catches up a year of monthly invoices, not a decade of
    /// weekly ones in a single request; the rest follow on the next run.
    /// </summary>
    public const int MaxCatchUp = 12;

    /// <summary>The <paramref name="index"/>th occurrence, the start date being the 0th.</summary>
    public static DateOnly Occurrence(DateOnly start, RecurrenceFrequency frequency, int every, int index)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(every, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        int step = checked(every * index);

        return frequency switch
        {
            RecurrenceFrequency.Weekly => start.AddDays(checked(7 * step)),
            RecurrenceFrequency.Monthly => start.AddMonths(step),
            RecurrenceFrequency.Quarterly => start.AddMonths(checked(3 * step)),
            RecurrenceFrequency.HalfYearly => start.AddMonths(checked(6 * step)),
            RecurrenceFrequency.Yearly => start.AddYears(step),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unknown frequency."),
        };
    }

    /// <summary>
    /// The occurrence a schedule raises next, or null when it has none left —
    /// past its end date, or past the number of occurrences it was set to raise.
    /// </summary>
    public static DateOnly? Next(
        DateOnly start,
        RecurrenceFrequency frequency,
        int every,
        int index,
        DateOnly? endDate,
        int? maxOccurrences)
    {
        if (maxOccurrences is int max && index >= max)
        {
            return null;
        }

        DateOnly next = Occurrence(start, frequency, every, index);
        return endDate is DateOnly end && next > end ? null : next;
    }

    /// <summary>
    /// The first occurrence index at or after <paramref name="fromIndex"/> whose
    /// date is not before <paramref name="today"/>. A schedule resumed after a
    /// pause skips what fell due while it was paused rather than raising it all
    /// at once.
    /// </summary>
    public static int FirstIndexOnOrAfter(
        DateOnly start, RecurrenceFrequency frequency, int every, int fromIndex, DateOnly today)
    {
        int index = fromIndex;
        while (Occurrence(start, frequency, every, index) < today)
        {
            index++;
        }

        return index;
    }
}

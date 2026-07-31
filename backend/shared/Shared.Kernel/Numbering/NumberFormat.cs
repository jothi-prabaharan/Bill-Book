namespace Shared.Kernel.Numbering;

/// <summary>
/// Composes a generated code from a series and a counter value, and works out
/// when a series is due to restart. Static and side-effect free so the API can
/// render a live preview with exactly the code that allocation will use — a
/// second implementation for the preview would eventually disagree with the
/// real one, and the user would only find out after the number was on a document.
/// </summary>
public static class NumberFormat
{
    /// <summary>
    /// Builds the code. Segments are prefix, financial year, branch code and the
    /// padded number, joined by the separator; the suffix is appended directly.
    /// Empty segments are skipped, so a separator never doubles up.
    /// </summary>
    public static string Compose(
        NumberingSeries series, long number, DateOnly onDate, int financialYearStartMonth)
    {
        var segments = new List<string>(4);

        if (!string.IsNullOrWhiteSpace(series.Prefix))
        {
            segments.Add(series.Prefix);
        }

        if (series.IncludeFinancialYear)
        {
            segments.Add(FinancialYear(onDate, financialYearStartMonth, series.FinancialYearFormat));
        }

        if (series.IncludeBranchCode && !string.IsNullOrWhiteSpace(series.BranchCode))
        {
            segments.Add(series.BranchCode);
        }

        segments.Add(number.ToString().PadLeft(series.NumberLength, '0'));

        return string.Join(series.Separator ?? string.Empty, segments) + (series.Suffix ?? string.Empty);
    }

    /// <summary>
    /// The financial year containing <paramref name="onDate"/>, rendered per the
    /// series format. A date on or after the start month belongs to the year that
    /// begins in it; anything earlier belongs to the year before.
    /// </summary>
    public static string FinancialYear(
        DateOnly onDate, int financialYearStartMonth, FinancialYearFormat format)
    {
        int startYear = onDate.Month >= financialYearStartMonth ? onDate.Year : onDate.Year - 1;
        int endYear = startYear + 1;

        return format switch
        {
            FinancialYearFormat.FullYearRange => $"{startYear}-{endYear % 100:00}",
            FinancialYearFormat.ShortYearRange => $"{startYear % 100:00}-{endYear % 100:00}",
            FinancialYearFormat.Compact => $"{startYear % 100:00}{endYear % 100:00}",
            FinancialYearFormat.StartYear => startYear.ToString(),
            _ => $"{startYear % 100:00}{endYear % 100:00}",
        };
    }

    /// <summary>
    /// Whether the counter is due to restart before this allocation. Yearly is
    /// measured against the financial year, so a series resetting on 1 April
    /// does not restart again on 1 January.
    /// </summary>
    public static bool IsResetDue(NumberingSeries series, DateOnly onDate, int financialYearStartMonth)
    {
        if (series.ResetFrequency == NumberResetFrequency.Never)
        {
            return false;
        }

        // Never reset before: the first allocation establishes the period rather
        // than restarting a counter that has not moved yet.
        if (series.LastResetOn is not DateOnly last)
        {
            return false;
        }

        return series.ResetFrequency switch
        {
            NumberResetFrequency.Yearly =>
                FinancialYearStart(last, financialYearStartMonth)
                    != FinancialYearStart(onDate, financialYearStartMonth),
            NumberResetFrequency.Monthly => last.Year != onDate.Year || last.Month != onDate.Month,
            NumberResetFrequency.Daily => last != onDate,
            _ => false,
        };
    }

    private static DateOnly FinancialYearStart(DateOnly onDate, int financialYearStartMonth)
    {
        int startYear = onDate.Month >= financialYearStartMonth ? onDate.Year : onDate.Year - 1;
        return new DateOnly(startYear, financialYearStartMonth, 1);
    }
}

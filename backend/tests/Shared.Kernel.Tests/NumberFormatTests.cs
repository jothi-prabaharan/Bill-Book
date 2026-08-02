using Shared.Kernel.Numbering;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// Numbering is worth testing before anything else in this project, because its
/// failures are silent. A wrong financial year segment produces a number that
/// looks entirely normal — <c>INV/2526/00042</c> reads fine whether or not 2526
/// is the right year — and the error surfaces at audit, long after the document
/// went out.
///
/// The same code composes the live preview on the Numbering series screen and
/// the number actually allocated, which is the reason it is static and pure.
/// These tests are what keep it that way.
/// </summary>
public class NumberFormatTests
{
    private static NumberingSeries Series(Action<NumberingSeries>? configure = null)
    {
        var series = new NumberingSeries
        {
            SeriesCode = "INVOICE",
            SeriesName = "Sales Invoice",
            Prefix = "INV",
            Separator = "/",
            NumberLength = 5,
        };

        configure?.Invoke(series);
        return series;
    }

    // ---- Compose ------------------------------------------------------------

    [Fact]
    public void Compose_PadsTheNumberToTheSeriesLength()
    {
        string code = NumberFormat.Compose(Series(), 42, new DateOnly(2026, 6, 1), 4);

        Assert.Equal("INV/00042", code);
    }

    [Fact]
    public void Compose_DoesNotTruncateANumberLongerThanThePadding()
    {
        // PadLeft only ever adds. A series that outgrows its width keeps
        // counting rather than wrapping back to 00001 and colliding.
        string code = NumberFormat.Compose(
            Series(s => s.NumberLength = 3), 123456, new DateOnly(2026, 6, 1), 4);

        Assert.Equal("INV/123456", code);
    }

    [Fact]
    public void Compose_BuildsTheFullIndianInvoiceShape()
    {
        NumberingSeries series = Series(s =>
        {
            s.IncludeFinancialYear = true;
            s.FinancialYearFormat = FinancialYearFormat.Compact;
            s.IncludeBranchCode = true;
            s.BranchCode = "CHN";
        });

        string code = NumberFormat.Compose(series, 42, new DateOnly(2025, 6, 1), 4);

        Assert.Equal("INV/2526/CHN/00042", code);
    }

    [Fact]
    public void Compose_SkipsAnEmptyPrefixWithoutDoublingTheSeparator()
    {
        // A leading "/00042" would be the giveaway that the join is naive.
        string code = NumberFormat.Compose(
            Series(s => s.Prefix = null), 42, new DateOnly(2026, 6, 1), 4);

        Assert.Equal("00042", code);
    }

    [Fact]
    public void Compose_SkipsTheBranchCodeWhenThereIsNoneToRender()
    {
        // IncludeBranchCode with nothing stored would otherwise emit an empty
        // segment, and two branches would produce identical numbers.
        NumberingSeries series = Series(s =>
        {
            s.IncludeBranchCode = true;
            s.BranchCode = null;
        });

        string code = NumberFormat.Compose(series, 42, new DateOnly(2026, 6, 1), 4);

        Assert.Equal("INV/00042", code);
    }

    [Fact]
    public void Compose_AppendsTheSuffixWithoutASeparator()
    {
        string code = NumberFormat.Compose(
            Series(s => s.Suffix = "-A"), 42, new DateOnly(2026, 6, 1), 4);

        Assert.Equal("INV/00042-A", code);
    }

    // ---- FinancialYear ------------------------------------------------------

    [Theory]
    [InlineData(2025, 4, 1, FinancialYearFormat.Compact, "2526")]
    [InlineData(2025, 4, 1, FinancialYearFormat.FullYearRange, "2025-26")]
    [InlineData(2025, 4, 1, FinancialYearFormat.ShortYearRange, "25-26")]
    [InlineData(2025, 4, 1, FinancialYearFormat.StartYear, "2025")]
    public void FinancialYear_RendersEachFormat(
        int year, int month, int day, FinancialYearFormat format, string expected)
    {
        string rendered = NumberFormat.FinancialYear(new DateOnly(year, month, day), 4, format);

        Assert.Equal(expected, rendered);
    }

    [Fact]
    public void FinancialYear_TreatsThirtyFirstOfMarchAsTheOldYear()
    {
        // The boundary itself. One day either side of it must land in different
        // years, or a series resets on the wrong date.
        Assert.Equal(
            "2526",
            NumberFormat.FinancialYear(new DateOnly(2026, 3, 31), 4, FinancialYearFormat.Compact));
    }

    [Fact]
    public void FinancialYear_TreatsFirstOfAprilAsTheNewYear()
    {
        Assert.Equal(
            "2627",
            NumberFormat.FinancialYear(new DateOnly(2026, 4, 1), 4, FinancialYearFormat.Compact));
    }

    [Fact]
    public void FinancialYear_FollowsTheBranchStartMonthRatherThanAssumingApril()
    {
        // A branch on a calendar year. This is the bug master.md 5.3 fixed: April was
        // assumed for everyone, so a January-start branch was issued the wrong
        // year segment and its counters reset in the wrong month.
        Assert.Equal(
            "2025-26",
            NumberFormat.FinancialYear(new DateOnly(2025, 3, 31), 1, FinancialYearFormat.FullYearRange));
    }

    // ---- IsResetDue ---------------------------------------------------------

    [Fact]
    public void IsResetDue_IsFalseForASeriesThatNeverResets()
    {
        NumberingSeries series = Series(s =>
        {
            s.ResetFrequency = NumberResetFrequency.Never;
            s.LastResetOn = new DateOnly(2020, 1, 1);
        });

        Assert.False(NumberFormat.IsResetDue(series, new DateOnly(2026, 6, 1), 4));
    }

    [Fact]
    public void IsResetDue_IsFalseOnTheFirstEverAllocation()
    {
        // Nothing has been issued, so there is no counter to restart. Returning
        // true here would consume a reset before the series had produced a
        // single number.
        NumberingSeries series = Series(s =>
        {
            s.ResetFrequency = NumberResetFrequency.Yearly;
            s.LastResetOn = null;
        });

        Assert.False(NumberFormat.IsResetDue(series, new DateOnly(2026, 6, 1), 4));
    }

    [Fact]
    public void IsResetDue_YearlyDoesNotFireOnTheFirstOfJanuary()
    {
        // The whole point of measuring yearly against the financial year. A
        // calendar comparison would restart an Indian invoice series in the
        // middle of its year, which breaks consecutive numbering.
        NumberingSeries series = Series(s =>
        {
            s.ResetFrequency = NumberResetFrequency.Yearly;
            s.LastResetOn = new DateOnly(2025, 4, 1);
        });

        Assert.False(NumberFormat.IsResetDue(series, new DateOnly(2026, 1, 1), 4));
    }

    [Fact]
    public void IsResetDue_YearlyFiresOnTheFirstOfApril()
    {
        NumberingSeries series = Series(s =>
        {
            s.ResetFrequency = NumberResetFrequency.Yearly;
            s.LastResetOn = new DateOnly(2025, 4, 1);
        });

        Assert.True(NumberFormat.IsResetDue(series, new DateOnly(2026, 4, 1), 4));
    }

    [Fact]
    public void IsResetDue_MonthlyFiresOnTheSameDayOfTheNextMonth()
    {
        NumberingSeries series = Series(s =>
        {
            s.ResetFrequency = NumberResetFrequency.Monthly;
            s.LastResetOn = new DateOnly(2026, 6, 15);
        });

        Assert.False(NumberFormat.IsResetDue(series, new DateOnly(2026, 6, 30), 4));
        Assert.True(NumberFormat.IsResetDue(series, new DateOnly(2026, 7, 1), 4));
    }

    [Fact]
    public void IsResetDue_MonthlyFiresAcrossAYearBoundary()
    {
        // Comparing month alone would say December and December are the same
        // month and skip the reset entirely.
        NumberingSeries series = Series(s =>
        {
            s.ResetFrequency = NumberResetFrequency.Monthly;
            s.LastResetOn = new DateOnly(2025, 12, 31);
        });

        Assert.True(NumberFormat.IsResetDue(series, new DateOnly(2026, 12, 1), 4));
    }

    [Fact]
    public void IsResetDue_DailyFiresOnTheNextDayAndNotTheSameOne()
    {
        NumberingSeries series = Series(s =>
        {
            s.ResetFrequency = NumberResetFrequency.Daily;
            s.LastResetOn = new DateOnly(2026, 6, 15);
        });

        Assert.False(NumberFormat.IsResetDue(series, new DateOnly(2026, 6, 15), 4));
        Assert.True(NumberFormat.IsResetDue(series, new DateOnly(2026, 6, 16), 4));
    }
}

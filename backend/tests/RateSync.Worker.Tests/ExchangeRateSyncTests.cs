using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using RateSync.Worker.Consumers;
using RateSync.Worker.Rbi;
using Shared.Kernel.Errors;
using Xunit;

namespace RateSync.Worker.Tests;

/// <summary>
/// The daily sync against a real master database (TK-26): the day's rates land
/// in <c>rat.ExchangeRates</c> with their date, a second run that day writes
/// nothing, and a page that cannot be read records an open failed run and no
/// rate.
///
/// Each test runs on a day of its own — the fake clock picks one in a year no
/// real rate is for — and clears it first, so a rerun starts clean.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class ExchangeRateSyncTests
{
    private readonly AdminFixture _admin;

    public ExchangeRateSyncTests(AdminFixture admin) => _admin = admin;

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubPage(string html) : IReferenceRatePageSource
    {
        public int Fetches { get; private set; }

        public Task<string> FetchAsync(CancellationToken ct)
        {
            Fetches++;
            return Task.FromResult(html);
        }
    }

    private sealed class FailingPage : IReferenceRatePageSource
    {
        public Task<string> FetchAsync(CancellationToken ct) => throw new HttpRequestException("503 Service Unavailable");
    }

    private static string Page(DateOnly date, decimal usd) =>
        $"<h3>Reference Rate as on {date:dd MMMM yyyy}</h3><table><tr><td>INR / 1 USD</td><td>{usd}</td></tr>"
        + "<tr><td>INR / 100 JPY</td><td>57.61</td></tr></table>";

    /// <summary>14:00 in India on the given day.</summary>
    private static DateTimeOffset Afternoon(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(new TimeOnly(14, 0)), ExchangeRateSync.IndiaOffset);

    private static async Task ClearAsync(AdminDbContext db, DateOnly day)
    {
        await db.ExchangeRates.Where(r => r.RateDate == day && r.Source == RateSource.Rbi).ExecuteDeleteAsync();
        await db.RateFetchRuns.Where(r => r.RunDate == day && r.Source == RateSource.Rbi).ExecuteDeleteAsync();
    }

    [SkippableFact]
    public async Task The_days_rates_land_with_their_date_and_a_second_run_writes_nothing()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        DateOnly day = new(1998, 3, 2);
        await using AdminDbContext db = _admin.CreateContext();
        await ClearAsync(db, day);

        var page = new StubPage(Page(day, 39.5m));
        var sync = new ExchangeRateSync(db, page, new FixedClock(Afternoon(day)));

        Assert.Equal(ExchangeRateSyncOutcome.Succeeded, await sync.RunAsync(default));

        List<ExchangeRate> rates = await db.ExchangeRates.AsNoTracking()
            .Where(r => r.RateDate == day && r.Source == RateSource.Rbi).ToListAsync();
        Assert.Equal(2, rates.Count);
        Assert.All(rates, r => Assert.Equal("INR", r.ToCurrencyCode));
        Assert.Equal(39.5m, rates.Single(r => r.FromCurrencyCode == "USD").Rate);
        Assert.Equal(0.5761m, rates.Single(r => r.FromCurrencyCode == "JPY").Rate);

        RateFetchRun run = await db.RateFetchRuns.AsNoTracking().SingleAsync(r => r.RunDate == day);
        Assert.Equal(RateFetchStatus.Succeeded, run.Status);
        Assert.Equal(2, run.RatesWritten);
        Assert.Equal(day, run.RateDate);
        Assert.Null(run.FollowUpStatus);

        // The same day again: nothing fetched, nothing written.
        Assert.Equal(ExchangeRateSyncOutcome.AlreadyDone, await sync.RunAsync(default));
        Assert.Equal(1, page.Fetches);
        Assert.Equal(2, await db.ExchangeRates.CountAsync(r => r.RateDate == day && r.Source == RateSource.Rbi));
    }

    [SkippableFact]
    public async Task Rates_already_on_file_for_the_pages_date_are_not_written_again()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        // A holiday: the page still shows Friday's rates, already stored on Friday.
        DateOnly friday = new(1998, 3, 6);
        DateOnly saturday = friday.AddDays(1);
        await using AdminDbContext db = _admin.CreateContext();
        await ClearAsync(db, friday);
        await ClearAsync(db, saturday);

        var page = new StubPage(Page(friday, 39.6m));
        Assert.Equal(ExchangeRateSyncOutcome.Succeeded,
            await new ExchangeRateSync(db, page, new FixedClock(Afternoon(friday))).RunAsync(default));
        Assert.Equal(ExchangeRateSyncOutcome.Succeeded,
            await new ExchangeRateSync(db, page, new FixedClock(Afternoon(saturday))).RunAsync(default));

        RateFetchRun second = await db.RateFetchRuns.AsNoTracking().SingleAsync(r => r.RunDate == saturday);
        Assert.Equal(0, second.RatesWritten);
        Assert.Equal(friday, second.RateDate);
        Assert.Equal(2, await db.ExchangeRates.CountAsync(r => r.RateDate == friday && r.Source == RateSource.Rbi));
    }

    [SkippableFact]
    public async Task A_failed_fetch_writes_no_rate_and_leaves_an_open_run_to_follow_up()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        DateOnly day = new(1998, 3, 9);
        await using AdminDbContext db = _admin.CreateContext();
        await ClearAsync(db, day);

        Assert.Equal(ExchangeRateSyncOutcome.Failed,
            await new ExchangeRateSync(db, new FailingPage(), new FixedClock(Afternoon(day))).RunAsync(default));
        Assert.Equal(ExchangeRateSyncOutcome.Failed,
            await new ExchangeRateSync(db, new StubPage("<p>Service unavailable</p>"), new FixedClock(Afternoon(day))).RunAsync(default));

        List<RateFetchRun> runs = await db.RateFetchRuns.AsNoTracking().Where(r => r.RunDate == day).ToListAsync();
        Assert.Equal(2, runs.Count);
        Assert.All(runs, r =>
        {
            Assert.Equal(RateFetchStatus.Failed, r.Status);
            Assert.Equal(ErrorFollowUpStatus.Open, r.FollowUpStatus);
            Assert.False(string.IsNullOrEmpty(r.Error));
        });
        Assert.Equal(0, await db.ExchangeRates.CountAsync(r => r.RateDate == day && r.Source == RateSource.Rbi));

        // A failed day is not a done day: the next tick fetches again.
        var page = new StubPage(Page(day, 39.7m));
        Assert.Equal(ExchangeRateSyncOutcome.Succeeded,
            await new ExchangeRateSync(db, page, new FixedClock(Afternoon(day))).RunAsync(default));
    }

    [Theory]
    [InlineData(13, 44, false)]
    [InlineData(13, 45, true)]
    [InlineData(23, 59, true)]
    public void The_sync_is_due_after_the_publication_time_in_india(int hour, int minute, bool due)
    {
        DateTimeOffset india = new(2026, 9, 24, hour, minute, 0, ExchangeRateSync.IndiaOffset);

        // The same instant expressed in UTC must give the same answer.
        Assert.Equal(due, RateSyncWorker.IsDue(india.ToUniversalTime(), new TimeOnly(13, 45)));
    }

    [Fact]
    public void The_day_is_indias_even_when_utc_has_not_turned_over()
    {
        // 20:00 UTC on the 23rd is 01:30 on the 24th in India.
        Assert.Equal(new DateOnly(2026, 9, 24),
            ExchangeRateSync.IndiaToday(new DateTimeOffset(2026, 9, 23, 20, 0, 0, TimeSpan.Zero)));
    }
}

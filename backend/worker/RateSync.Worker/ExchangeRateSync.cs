using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using RateSync.Worker.Rbi;
using Shared.Kernel.Errors;

namespace RateSync.Worker;

/// <summary>What one sync came to.</summary>
public enum ExchangeRateSyncOutcome
{
    /// <summary>A run for this day already succeeded; nothing was fetched.</summary>
    AlreadyDone = 0,
    Succeeded = 1,
    Failed = 2,
}

/// <summary>
/// The daily RBI reference-rate sync (D-03, TK-26): fetch the page, read it,
/// and add each rate to <c>rat.ExchangeRates</c> as <c>{code} → INR</c> with
/// source Rbi, dated as the page dates it.
///
/// <b>Idempotent per day, twice over.</b> A succeeded run for the day stops a
/// second one before anything is fetched; and if one does run, a rate already
/// on file for that pair, date and source is left as it is, so it writes
/// nothing. RBI does not revise a published reference rate, and a hand-entered
/// correction is a separate Manual row that outranks this one.
///
/// <b>A failure writes no rate.</b> It is recorded in <c>rat.RateFetchRuns</c>
/// with <c>FollowUpStatus</c> Open, and the next tick tries again.
/// </summary>
public sealed class ExchangeRateSync
{
    /// <summary>India does not observe daylight saving, so a fixed offset is exact.</summary>
    public static readonly TimeSpan IndiaOffset = TimeSpan.FromHours(5.5);

    private readonly AdminDbContext _db;
    private readonly IReferenceRatePageSource _page;
    private readonly TimeProvider _clock;

    public ExchangeRateSync(AdminDbContext db, IReferenceRatePageSource page, TimeProvider clock)
    {
        _db = db;
        _page = page;
        _clock = clock;
    }

    /// <summary>The calendar day in India at this instant.</summary>
    public static DateOnly IndiaToday(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(IndiaOffset).DateTime);

    public async Task<ExchangeRateSyncOutcome> RunAsync(CancellationToken ct)
    {
        DateTimeOffset started = _clock.GetUtcNow();
        DateOnly runDate = IndiaToday(started);

        bool done = await _db.RateFetchRuns.AnyAsync(
            r => r.Source == RateSource.Rbi && r.RunDate == runDate && r.Status == RateFetchStatus.Succeeded, ct);
        if (done)
        {
            return ExchangeRateSyncOutcome.AlreadyDone;
        }

        var run = new RateFetchRun { Source = RateSource.Rbi, RunDate = runDate, StartedAt = started };

        try
        {
            ReferenceRatePage page = RbiReferenceRateParser.Parse(await _page.FetchAsync(ct));
            run.RateDate = page.RateDate;
            run.RatesWritten = await AddMissingAsync(page, ct);
            run.Status = RateFetchStatus.Succeeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing half-written survives: the tracked rows are dropped before
            // the run is recorded, so the failure row is the only thing saved.
            _db.ChangeTracker.Clear();
            run.Status = RateFetchStatus.Failed;
            run.RatesWritten = 0;
            run.Error = Truncate($"{ex.GetType().Name}: {ex.Message}", 2000);
            run.FollowUpStatus = ErrorFollowUpStatus.Open;
        }

        run.FinishedAt = _clock.GetUtcNow();
        _db.RateFetchRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        return run.Status == RateFetchStatus.Succeeded ? ExchangeRateSyncOutcome.Succeeded : ExchangeRateSyncOutcome.Failed;
    }

    private async Task<int> AddMissingAsync(ReferenceRatePage page, CancellationToken ct)
    {
        string[] codes = page.Rates.Select(r => r.CurrencyCode).ToArray();

        HashSet<string> onFile = (await _db.ExchangeRates
            .Where(r => r.ToCurrencyCode == "INR" && r.RateDate == page.RateDate && r.Source == RateSource.Rbi
                && codes.Contains(r.FromCurrencyCode))
            .Select(r => r.FromCurrencyCode)
            .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        int written = 0;
        foreach (ReferenceRate rate in page.Rates.Where(r => r.CurrencyCode != "INR" && !onFile.Contains(r.CurrencyCode)))
        {
            _db.ExchangeRates.Add(new ExchangeRate
            {
                FromCurrencyCode = rate.CurrencyCode,
                ToCurrencyCode = "INR",
                RateDate = page.RateDate,
                Rate = rate.RateInInr,
                Source = RateSource.Rbi,
            });
            written++;
        }

        return written;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

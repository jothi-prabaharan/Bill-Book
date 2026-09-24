using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using RateSync.Worker.Ibja;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Errors;

namespace RateSync.Worker;

/// <summary>
/// The daily IBJA metal rate sync (D-14, TK-25): fetch the API and add each
/// rate to <c>rat.MetalRates</c> with source Ibja, dated as the API dates it.
/// </summary>
public sealed class IbjaMetalRateSync
{
    private readonly AdminDbContext _db;
    private readonly IIbjaClient _client;
    private readonly ISecretStore _secrets;
    private readonly TimeProvider _clock;
    private readonly ILogger<IbjaMetalRateSync> _logger;

    public IbjaMetalRateSync(AdminDbContext db, IIbjaClient client, ISecretStore secrets, TimeProvider clock, ILogger<IbjaMetalRateSync> logger)
    {
        _db = db;
        _client = client;
        _secrets = secrets;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ExchangeRateSyncOutcome> RunAsync(CancellationToken ct)
    {
        DateTimeOffset started = _clock.GetUtcNow();
        DateOnly runDate = ExchangeRateSync.IndiaToday(started);

        bool done = await _db.RateFetchRuns.AnyAsync(
            r => r.Source == RateSource.Ibja && r.RunDate == runDate && r.Status == RateFetchStatus.Succeeded, ct);
        if (done)
        {
            return ExchangeRateSyncOutcome.AlreadyDone;
        }
        
        string apiKey = await _secrets.GetSecretAsync("IbjaApiKey", ct);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("IBJA API key is missing from ISecretStore under 'IbjaApiKey'. Cannot fetch metal rates.");
            return ExchangeRateSyncOutcome.Failed;
        }

        var run = new RateFetchRun { Source = RateSource.Ibja, RunDate = runDate, StartedAt = started };

        try
        {
            IbjaRates page = await _client.FetchRatesAsync(apiKey, ct);
            run.RateDate = page.Date;
            run.RatesWritten = await AddMissingAsync(page, ct);
            run.Status = RateFetchStatus.Succeeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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

    private async Task<int> AddMissingAsync(IbjaRates page, CancellationToken ct)
    {
        var newRates = new List<MetalRate>();
        foreach(var rate in page.Rates)
        {
            if (TryParseMetalAndPurity(rate.Key, out Metal metal, out string purityCode))
            {
                newRates.Add(new MetalRate 
                { 
                    Metal = metal, 
                    PurityCode = purityCode, 
                    RateDate = page.Date, 
                    RatePerGram = rate.Value, 
                    Source = RateSource.Ibja 
                });
            }
        }

        if (newRates.Count == 0) return 0;

        string[] purities = newRates.Select(r => r.PurityCode).Distinct().ToArray();
        Metal[] metals = newRates.Select(r => r.Metal).Distinct().ToArray();

        HashSet<(Metal, string)> onFile = (await _db.MetalRates
            .Where(r => r.RateDate == page.Date && r.Source == RateSource.Ibja
                && metals.Contains(r.Metal) && purities.Contains(r.PurityCode))
            .Select(r => new { r.Metal, r.PurityCode })
            .ToListAsync(ct))
            .Select(x => (x.Metal, x.PurityCode))
            .ToHashSet();

        int written = 0;
        foreach (var rate in newRates)
        {
            if (!onFile.Contains((rate.Metal, rate.PurityCode)))
            {
                _db.MetalRates.Add(rate);
                written++;
            }
        }

        return written;
    }

    private static bool TryParseMetalAndPurity(string key, out Metal metal, out string purityCode)
    {
        key = key.ToLowerInvariant();
        metal = default;
        purityCode = "";

        if (key.StartsWith("gold")) metal = Metal.Gold;
        else if (key.StartsWith("silver")) metal = Metal.Silver;
        else if (key.StartsWith("platinum")) metal = Metal.Platinum;
        else return false;

        if (key.Contains("24k") || key.Contains("999") || key.Contains("995"))
        {
            purityCode = key.Contains("24k") ? "24K" : key.Contains("999") ? "999" : "995";
        }
        else if (key.Contains("22k") || key.Contains("916"))
        {
            purityCode = key.Contains("22k") ? "22K" : "916";
        }
        else if (key.Contains("750")) purityCode = "750";
        else if (key.Contains("585")) purityCode = "585";
        else return false;

        return true;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

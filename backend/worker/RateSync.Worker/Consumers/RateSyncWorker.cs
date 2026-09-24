namespace RateSync.Worker.Consumers;

/// <summary>
/// Runs the RBI sync once a day, after the rates are published (TK-26).
///
/// It wakes every <c>RateSync:CheckIntervalMinutes</c> (default 60) and runs the
/// sync once the time in India is past <c>RateSync:RbiAfter</c> (default 13:45 —
/// the reference rates are published early in the afternoon). A day that has
/// already succeeded costs one indexed query; a failed day is retried on the
/// next wake, so the interval is the backoff.
/// </summary>
public sealed class RateSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<RateSyncWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeOnly _rbiAfter;

    public RateSyncWorker(
        IServiceScopeFactory scopes, TimeProvider clock, IConfiguration configuration, ILogger<RateSyncWorker> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("RateSync:CheckIntervalMinutes", 60)));
        _rbiAfter = TimeOnly.TryParse(configuration["RateSync:RbiAfter"], out TimeOnly after) ? after : new TimeOnly(13, 45);
    }

    /// <summary>Whether the day's rates should be out by now, on India's clock.</summary>
    public static bool IsDue(DateTimeOffset now, TimeOnly after) =>
        TimeOnly.FromDateTime(now.ToOffset(ExchangeRateSync.IndiaOffset).DateTime) >= after;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval, _clock);

        do
        {
            if (IsDue(_clock.GetUtcNow(), _rbiAfter))
            {
                try
                {
                    await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
                    ExchangeRateSyncOutcome outcome =
                        await scope.ServiceProvider.GetRequiredService<ExchangeRateSync>().RunAsync(stoppingToken);

                    if (outcome != ExchangeRateSyncOutcome.AlreadyDone)
                    {
                        _logger.LogInformation("RBI reference-rate sync: {Outcome}.", outcome);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The run could not even be recorded — the database is down.
                    // Logged, and the next wake tries again.
                    _logger.LogError(ex, "RBI reference-rate sync could not run.");
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

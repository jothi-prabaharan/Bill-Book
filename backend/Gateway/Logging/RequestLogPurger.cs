using Gateway.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gateway.Logging;

/// <summary>
/// Deletes rows past the retention window. Without this the request log becomes
/// the largest table in the database and nobody notices until it is.
/// </summary>
public class RequestLogPurger(
    IServiceScopeFactory scopes,
    IOptions<RequestLogOptions> options,
    ILogger<RequestLogPurger> logger) : BackgroundService
{
    private readonly RequestLogOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || _options.RetentionDays <= 0)
        {
            return;
        }

        using PeriodicTimer timer = new(TimeSpan.FromHours(_options.PurgeIntervalHours));

        // Run once at startup, then on the interval. A gateway that restarts daily
        // would otherwise never reach the first tick.
        do
        {
            await PurgeAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = scopes.CreateScope();
            GatewayDbContext db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

            DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays);

            // ExecuteDeleteAsync issues a single set-based DELETE without loading
            // or tracking the rows. Still LINQ, no raw SQL.
            int deleted = await db.RequestLogs
                .Where(log => log.StartedAt < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                logger.LogInformation(
                    "Purged {Deleted} gateway request log entries older than {Cutoff:u}.", deleted, cutoff);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gateway request log purge failed; will retry on the next interval.");
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

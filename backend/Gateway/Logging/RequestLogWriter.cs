using Gateway.Entity.TableEntities;
using Gateway.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gateway.Logging;

/// <summary>
/// Drains the queue and writes batches. Flushes when the batch fills or the
/// interval elapses, whichever comes first, so a quiet gateway still persists
/// its handful of rows promptly.
///
/// A database failure here must never take the gateway down: the batch is logged
/// and dropped, and the loop continues.
/// </summary>
public class RequestLogWriter(
    IRequestLogQueue queue,
    IServiceScopeFactory scopes,
    IOptions<RequestLogOptions> options,
    ILogger<RequestLogWriter> logger) : BackgroundService
{
    private readonly RequestLogOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Gateway request logging is disabled.");
            return;
        }

        List<RequestLog> batch = new(_options.BatchSize);
        TimeSpan flushInterval = TimeSpan.FromSeconds(_options.FlushIntervalSeconds);
        using PeriodicTimer timer = new(flushInterval);

        Task<bool> tick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
        IAsyncEnumerator<RequestLog> entries = queue.ReadAllAsync(stoppingToken).GetAsyncEnumerator(stoppingToken);
        ValueTask<bool> pending = entries.MoveNextAsync();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Task<bool> next = pending.AsTask();
                Task completed = await Task.WhenAny(next, tick);

                if (completed == next)
                {
                    if (!await next)
                    {
                        break;
                    }

                    batch.Add(entries.Current);
                    pending = entries.MoveNextAsync();

                    if (batch.Count < _options.BatchSize)
                    {
                        continue;
                    }
                }
                else
                {
                    await tick;
                    tick = timer.WaitForNextTickAsync(stoppingToken).AsTask();

                    if (batch.Count == 0)
                    {
                        continue;
                    }
                }

                await FlushAsync(batch, stoppingToken);
                batch.Clear();
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
        finally
        {
            await entries.DisposeAsync();

            // Best effort on the way out, with a fresh token: stoppingToken is
            // already cancelled by this point and would abort the write.
            if (batch.Count > 0)
            {
                using CancellationTokenSource shutdown = new(TimeSpan.FromSeconds(5));
                await FlushAsync(batch, shutdown.Token);
            }
        }
    }

    private async Task FlushAsync(List<RequestLog> batch, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = scopes.CreateScope();
            GatewayDbContext db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

            db.RequestLogs.AddRange(batch);
            await db.SaveChangesAsync(ct);

            long dropped = queue.DroppedCount;
            if (dropped > 0)
            {
                logger.LogWarning(
                    "Gateway request log has dropped {Dropped} entries since start; the queue is saturating.",
                    dropped);
            }
        }
        catch (Exception ex)
        {
            // Deliberately swallowed. Losing request logs is survivable; crashing
            // the gateway because the log table is unreachable is not.
            logger.LogError(ex, "Failed to write {Count} gateway request log entries; batch discarded.", batch.Count);
        }
    }
}

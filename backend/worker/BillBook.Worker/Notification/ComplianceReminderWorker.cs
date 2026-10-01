using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BillBook.Worker.Notification.Reminders;

namespace BillBook.Worker.Notification;

/// <summary>
/// Sends statutory filing compliance reminders once a day to users who hold accounting.view (TK-119).
/// </summary>
public class ComplianceReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ComplianceReminderWorker> _logger;

    public ComplianceReminderWorker(
        IServiceScopeFactory scopes, ILogger<ComplianceReminderWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Public so a run can be driven without the schedule (e.g. in tests).</summary>
    public async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopes.CreateScope();
            var run = scope.ServiceProvider.GetRequiredService<ComplianceReminderRun>();
            int sent = await run.RunAsync(ct);
            if (sent > 0)
            {
                _logger.LogInformation("Sent {Count} compliance filing reminder emails.", sent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run compliance reminders.");
        }
    }
}


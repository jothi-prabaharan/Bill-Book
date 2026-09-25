using Notification.Worker.Reminders;
using Shared.Kernel.Tenancy;

namespace Notification.Worker;

/// <summary>
/// Walks every branch once a day and sends its AMC renewal reminders (S8,
/// TK-68), each branch in its own scope with its tenant set, as the payment
/// reminders do. A branch without School has no contracts and sends nothing.
/// </summary>
public class AmcRenewalReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ITenantEnumerator _branches;
    private readonly ILogger<AmcRenewalReminderWorker> _logger;

    public AmcRenewalReminderWorker(IServiceScopeFactory scopes, ITenantEnumerator branches, ILogger<AmcRenewalReminderWorker> logger)
    {
        _scopes = scopes;
        _branches = branches;
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

    /// <summary>Every branch, once. Public so a run can be driven without the schedule.</summary>
    public async Task RunOnceAsync(CancellationToken ct)
    {
        foreach (ActiveOrganization branch in await _branches.ListAsync(ct))
        {
            try
            {
                using IServiceScope scope = _scopes.CreateScope();
                var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
                tenant.CustomerId = branch.CustomerId;
                tenant.OrgId = branch.OrgId;

                AmcRenewalRunResult result = await scope.ServiceProvider.GetRequiredService<AmcRenewalReminderRun>().RunAsync(ct);
                if (result.Sent > 0 || result.NoEmail > 0)
                {
                    _logger.LogInformation("AMC renewal reminders for {OrgId}: {Sent} sent, {NoEmail} without an address.",
                        branch.OrgId, result.Sent, result.NoEmail);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "AMC renewal reminders for {OrgId} failed; tomorrow's run retries.", branch.OrgId);
            }
        }
    }
}

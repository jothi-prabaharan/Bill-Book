using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification.Worker.Reminders;
using Shared.Kernel.Tenancy;

namespace Notification.Worker;

/// <summary>
/// Sends payment reminders once a day, branch by branch (TK-20).
///
/// <b>Each branch runs in a scope of its own with its tenant set</b>, the way
/// the costing engine walks branches: the list comes from Master, and every
/// read below goes through the query filter and row-level security. Before
/// TK-20 this read every branch at once with <c>IgnoreQueryFilters</c> and no
/// tenant — which saw nothing at all once RLS was restored — wrote a log row
/// per overdue invoice, and sent no email.
///
/// One branch failing does not stop the others; the next day retries it, and
/// the reminders' fixed message ids keep a retry from sending twice.
/// </summary>
public class PaymentReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ITenantEnumerator _branches;
    private readonly ILogger<PaymentReminderWorker> _logger;

    public PaymentReminderWorker(
        IServiceScopeFactory scopes, ITenantEnumerator branches, ILogger<PaymentReminderWorker> logger)
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
        IReadOnlyList<ActiveOrganization> branches = await _branches.ListAsync(ct);

        foreach (ActiveOrganization branch in branches)
        {
            try
            {
                using IServiceScope scope = _scopes.CreateScope();

                var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
                tenant.CustomerId = branch.CustomerId;
                tenant.OrgId = branch.OrgId;

                ReminderRunResult result = await scope.ServiceProvider
                    .GetRequiredService<PaymentReminderRun>()
                    .RunAsync(ct);

                if (result.Sent > 0 || result.Stopped)
                {
                    _logger.LogInformation(
                        "Payment reminders for {OrgId}: {Sent} sent, {Paid} already paid, {NoEmail} without an email{Stopped}.",
                        branch.OrgId, result.Sent, result.AlreadyPaid, result.NoEmail,
                        result.Stopped ? "; stopped early" : "");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Payment reminders for {OrgId} failed; tomorrow's run retries.", branch.OrgId);
            }
        }
    }
}

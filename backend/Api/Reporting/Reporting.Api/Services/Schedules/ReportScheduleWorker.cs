using Reporting.Repository.Repositories;
using Shared.Kernel.Tenancy;

namespace Reporting.Api.Services;

/// <summary>
/// Every five minutes, each branch Master lists, in its own scope with its
/// tenant set, the way the payment reminders do (TK-114). Several replicas may
/// run it; the guarded claim in <see cref="ReportScheduleRunRepository"/> is what keeps
/// a schedule to one email per run.
/// </summary>
public sealed class ReportScheduleWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ITenantEnumerator _branches;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReportScheduleWorker> _log;

    public ReportScheduleWorker(
        IServiceScopeFactory scopes, ITenantEnumerator branches, TimeProvider clock, ILogger<ReportScheduleWorker> log)
    {
        _scopes = scopes;
        _branches = branches;
        _clock = clock;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(Interval, _clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        foreach (ActiveOrganization branch in await _branches.ListAsync(ct))
        {
            try
            {
                using IServiceScope scope = _scopes.CreateScope();

                TenantContext tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
                tenant.CustomerId = branch.CustomerId;
                tenant.OrgId = branch.OrgId;

                ScheduleRunResult result = await scope.ServiceProvider
                    .GetRequiredService<ReportScheduleRunRepository>()
                    .RunDueAsync(_clock.GetUtcNow(), ct);

                if (result.Claimed > 0)
                {
                    _log.LogInformation(
                        "Scheduled reports for {OrgId}: {Sent} sent, {Skipped} skipped, {Failed} failed.",
                        branch.OrgId, result.Sent, result.Skipped, result.Failed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Scheduled reports for {OrgId} failed; the next tick retries.", branch.OrgId);
            }
        }
    }
}

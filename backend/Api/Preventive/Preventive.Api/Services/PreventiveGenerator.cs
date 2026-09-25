using Shared.Kernel.Tenancy;

namespace Preventive.Api.Services;

/// <summary>
/// Walks every branch each hour and generates its due occurrences (S7, TK-67).
/// Each branch runs in its own scope with its tenant set, as the payment
/// reminders do. Two replicas running at once are safe: the claim is a guarded
/// update and the occurrence is unique on plan and due date.
/// </summary>
public sealed class PreventiveGenerator : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ITenantEnumerator _branches;
    private readonly TimeProvider _clock;
    private readonly ILogger<PreventiveGenerator> _log;

    public PreventiveGenerator(IServiceScopeFactory scopes, ITenantEnumerator branches, TimeProvider clock, ILogger<PreventiveGenerator> log)
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
                // Let the host finish starting before the first run.
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                await RunOnceAsync(stoppingToken);
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
        DateOnly today = DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

        foreach (ActiveOrganization branch in await _branches.ListAsync(ct))
        {
            try
            {
                using IServiceScope scope = _scopes.CreateScope();
                var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
                tenant.CustomerId = branch.CustomerId;
                tenant.OrgId = branch.OrgId;

                var result = await scope.ServiceProvider.GetRequiredService<PreventiveService>().GenerateAsync(today, ct);
                if (result.Generated > 0 || result.Raised > 0 || result.Failed > 0)
                {
                    _log.LogInformation("Preventive plans for {OrgId}: {Generated} generated, {Raised} raised, {Failed} to retry.",
                        branch.OrgId, result.Generated, result.Raised, result.Failed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Preventive plans for {OrgId} failed; the next run retries.", branch.OrgId);
            }
        }
    }
}

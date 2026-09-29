using Customer.Entity.Enums;
using Customer.Repository;
using Customer.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Tenancy;

namespace Customer.Api.Services;

public sealed class CampaignSendingWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ITenantEnumerator _branches;
    private readonly TimeProvider _clock;
    private readonly ILogger<CampaignSendingWorker> _log;

    public CampaignSendingWorker(
        IServiceScopeFactory scopes,
        ITenantEnumerator branches,
        TimeProvider clock,
        ILogger<CampaignSendingWorker> log)
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
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled error in CampaignSendingWorker.");
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<ActiveOrganization> branches;
        try
        {
            branches = await _branches.ListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not enumerate branches for campaign processing.");
            return;
        }

        DateTimeOffset now = _clock.GetUtcNow();

        foreach (var branch in branches)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
                tenant.CustomerId = branch.CustomerId;
                tenant.OrgId = branch.OrgId;

                var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
                var campaignRepo = scope.ServiceProvider.GetRequiredService<CampaignRepository>();

                var dueCampaignIds = await db.Campaigns
                    .Where(c => c.Status == CampaignStatus.Scheduled && c.ScheduledAt <= now)
                    .Select(c => c.CampaignId)
                    .ToListAsync(ct);

                foreach (long campaignId in dueCampaignIds)
                {
                    try
                    {
                        await campaignRepo.ExecuteCampaignSendAsync(campaignId, ct);
                        _log.LogInformation("Successfully processed scheduled campaign {CampaignId} for branch {OrgId}.", campaignId, branch.OrgId);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Failed to execute send for campaign {CampaignId} in branch {OrgId}.", campaignId, branch.OrgId);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Campaign check failed for branch {OrgId}; will retry next tick.", branch.OrgId);
            }
        }
    }
}

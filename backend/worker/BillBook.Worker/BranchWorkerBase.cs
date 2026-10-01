using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Errors;

namespace BillBook.Worker;

/// <summary>
/// A base class for background workers that run per active organization.
/// Automatically handles polling loops, fetching active branches from Master, 
/// creating scopes, setting the TenantContext, and auditing failures.
/// </summary>
public abstract class BranchWorkerBase<T> : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    protected readonly ILogger<T> _logger;
    protected readonly IConfiguration _config;
    
    protected virtual TimeSpan DefaultInterval => TimeSpan.FromMinutes(5);

    protected BranchWorkerBase(IServiceScopeFactory scopes, ILogger<T> logger, IConfiguration config)
    {
        _scopes = scopes;
        _logger = logger;
        _config = config;
    }

    protected abstract string JobName { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Per-job on/off switches in configuration (Workers:<Job>:Enabled)
            bool isEnabled = _config.GetValue<bool>($"Workers:{JobName}:Enabled", true);

            if (isEnabled)
            {
                await RunOnceAsync(stoppingToken);
            }

            try
            {
                // Fallback to DefaultInterval if no specific interval is set
                TimeSpan interval = _config.GetValue<TimeSpan?>($"Workers:{JobName}:Interval") ?? DefaultInterval;
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using IServiceScope listScope = _scopes.CreateScope();
        ITenantEnumerator enumerator = listScope.ServiceProvider.GetRequiredService<ITenantEnumerator>();
        
        IReadOnlyList<ActiveOrganization> branches = await enumerator.ListAsync(ct);

        foreach (ActiveOrganization branch in branches)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                using IServiceScope scope = _scopes.CreateScope();

                var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
                tenant.CustomerId = branch.CustomerId;
                tenant.OrgId = branch.OrgId;

                await ProcessBranchAsync(branch, scope, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "{Job} failed for branch {OrgId}", JobName, branch.OrgId);
                
                try 
                {
                    using IServiceScope errorScope = _scopes.CreateScope();
                    var errorTenant = errorScope.ServiceProvider.GetRequiredService<TenantContext>();
                    errorTenant.CustomerId = branch.CustomerId;
                    errorTenant.OrgId = branch.OrgId;

                    var auditor = errorScope.ServiceProvider.GetRequiredService<IWorkerErrorAuditor>();
                    await auditor.AuditAsync(JobName, null, ex, ct);
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "Failed to audit error for {Job} in branch {OrgId}", JobName, branch.OrgId);
                }
            }
        }
    }

    protected abstract Task ProcessBranchAsync(ActiveOrganization branch, IServiceScope scope, CancellationToken ct);
}

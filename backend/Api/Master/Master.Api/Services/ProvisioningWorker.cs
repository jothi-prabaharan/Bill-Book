using Microsoft.EntityFrameworkCore;
using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;

namespace Master.Api.Services;

/// <summary>
/// Background provisioner. Sequence per job: create the owner user →
/// seed the new organization's master data (CoA, tax master, numbering
/// series, units...) → mark Customer Trial and Organization Active.
///
/// No database to create — every customer already shares the one tenant
/// database, migrated once like mst. On any failure the customer is marked
/// Failed; the seed is idempotent, so an admin retry re-runs safely.
/// </summary>
public sealed class ProvisioningWorker : BackgroundService
{
    private readonly IProvisioningQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ProvisioningWorker> _logger;

    public ProvisioningWorker(
        IProvisioningQueue queue,
        IServiceScopeFactory scopes,
        ILogger<ProvisioningWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ProvisioningJob job;
            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await ProvisionAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provisioning failed for customer {CustomerId}", job.CustomerId);
                await MarkFailedAsync(job.CustomerId, stoppingToken);
            }
        }
    }

    private async Task ProvisionAsync(ProvisioningJob job, CancellationToken ct)
    {
        using IServiceScope scope = _scopes.CreateScope();
        AdminDbContext db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        IIdentityAdmin identity = scope.ServiceProvider.GetRequiredService<IIdentityAdmin>();

        // 1. Owner user + Owner role assignment, via Identity's internal API.
        await identity.CreateOwnerUserAsync(new CreateOwnerUser(
            job.OrgId, job.OwnerEmail, job.OwnerDisplayName,
            job.OwnerMobileNumber, job.OwnerPassword), ct);

        // 2. Seed the organization's master data directly — chart of accounts,
        //    tax master, numbering series, units. The tenant database itself
        //    already exists and is already migrated; there is nothing else to
        //    create before this can run.
        ITenantSeeder seeder = scope.ServiceProvider.GetRequiredService<ITenantSeeder>();
        IReadOnlyList<string> unseeded = await seeder.SeedAsync(job.CustomerId, job.OrgId, ct);

        // An organization missing its master data is not ready, whatever the
        // rest of provisioning did: no journal can post without a chart of
        // accounts, and no item can be saved without a unit type. Failing here
        // sends it to admin retry instead of handing the customer an account
        // that looks finished and is not. Every seed is idempotent, so the
        // retry is safe.
        if (unseeded.Count > 0)
        {
            throw new InvalidOperationException(
                $"Provisioning could not seed: {string.Join(", ", unseeded)}. "
                + "The organization has been left unprovisioned for retry.");
        }

        // 3. Flip statuses; login stays blocked until this commits.
        Customer customer = await db.Customers.FirstAsync(c => c.CustomerId == job.CustomerId, ct);
        Organization org = await db.Organizations.FirstAsync(o => o.OrgId == job.OrgId, ct);

        customer.Status = TenantStatus.Trial;
        org.Status = TenantStatus.Active;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Provisioned customer {CustomerId}", job.CustomerId);
    }

    private async Task MarkFailedAsync(Guid customerId, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopes.CreateScope();
            AdminDbContext db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            Customer? customer = await db.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
            if (customer is not null)
            {
                customer.Status = TenantStatus.Failed;
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not mark customer {CustomerId} as failed", customerId);
        }
    }
}

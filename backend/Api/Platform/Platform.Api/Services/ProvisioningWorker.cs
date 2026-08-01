using Microsoft.EntityFrameworkCore;
using Platform.Entity.Enums;
using Platform.Entity.TableEntities;
using Platform.Repository;
using Shared.Kernel.Interfaces;

namespace Platform.Api.Services;

/// <summary>Published when a tenant database is ready; each service migrates its own schema on this.</summary>
public sealed record CustomerProvisioned(Guid CustomerId, Guid OrgId, string DatabaseName);

/// <summary>
/// Background provisioner. Sequence per job:
/// CREATE DATABASE (raw SQL — allowed exception) → store connection in the
/// secret store → create the owner user via Identity → publish
/// CustomerProvisioned (consumers migrate per-customer schemas and seed CoA /
/// Tax Master) → mark Customer Trial-active and the database Ready.
/// On any failure the database row is marked Failed for admin retry.
/// </summary>
public sealed class ProvisioningWorker : BackgroundService
{
    private readonly IProvisioningQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<ProvisioningWorker> _logger;
    private readonly TimeProvider _clock;

    public ProvisioningWorker(
        IProvisioningQueue queue,
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILogger<ProvisioningWorker> logger,
        TimeProvider clock)
    {
        _queue = queue;
        _scopes = scopes;
        _config = config;
        _logger = logger;
        _clock = clock;
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
        PlatformDbContext db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        ISecretStore secrets = scope.ServiceProvider.GetRequiredService<ISecretStore>();
        IIdentityAdmin identity = scope.ServiceProvider.GetRequiredService<IIdentityAdmin>();
        IEventPublisher events = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        // 1. CREATE DATABASE — raw SQL is allowed here (no LINQ equivalent).
        //    The name is generated (prefix + digits), never user input.
        string safeName = new(job.DatabaseName.Where(char.IsLetterOrDigit).ToArray());
#pragma warning disable EF1002 // CREATE DATABASE takes no parameters; safeName is stripped to letters and digits above.
        await db.Database.ExecuteSqlRawAsync(
            $"CREATE DATABASE \"{safeName}\" ENCODING 'UTF8'", ct);
#pragma warning restore EF1002

        // 2. Store the tenant connection string — Key Vault, never plt.
        //    Guarded rather than defaulted: an unset value here would store an empty
        //    connection string and step 5 would still flip the org to Active, telling
        //    the customer their account is ready when it can never connect.
        string template = _config["TenantDatabase:ConnectionTemplate"] is { Length: > 0 } configured
            ? configured
            : throw new InvalidOperationException(
                "TenantDatabase:ConnectionTemplate is not configured. Set it in " +
                "appsettings.{Environment}.json or via the TenantDatabase__ConnectionTemplate " +
                "environment variable.");
        await secrets.SetSecretAsync(
            $"tenant-db-{safeName}", string.Format(template, safeName), ct);

        // 3. Owner user + Owner role assignment, via Identity's internal API.
        await identity.CreateOwnerUserAsync(new CreateOwnerUser(
            job.OrgId, job.OwnerEmail, job.OwnerDisplayName,
            job.OwnerMobileNumber, job.OwnerPassword), ct);

        // 4. Fan out. The event is published for whatever consumes it later, but
        //    it is not what seeds the organization: the only IEventPublisher
        //    implementation logs and drops, so relying on it left every new
        //    organization with no chart of accounts, no units and no numbering.
        //    The seeder calls each service directly and is the step that counts.
        await events.PublishAsync(new CustomerProvisioned(
            job.CustomerId, job.OrgId, safeName), ct);

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

        // 5. Flip statuses; login stays blocked until this commits.
        Customer customer = await db.Customers.FirstAsync(c => c.CustomerId == job.CustomerId, ct);
        Organization org = await db.Organizations.FirstAsync(o => o.OrgId == job.OrgId, ct);
        CustomerDatabase database = await db.CustomerDatabases.FirstAsync(d => d.CustomerId == job.CustomerId, ct);

        customer.Status = TenantStatus.Trial;
        org.Status = TenantStatus.Active;
        database.Status = ProvisioningStatus.Ready;
        database.ProvisionedAt = _clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Provisioned customer {CustomerId} → database {Database}", job.CustomerId, safeName);
    }

    private async Task MarkFailedAsync(Guid customerId, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopes.CreateScope();
            PlatformDbContext db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            CustomerDatabase? database = await db.CustomerDatabases
                .FirstOrDefaultAsync(d => d.CustomerId == customerId, ct);
            if (database is not null)
            {
                database.Status = ProvisioningStatus.Failed;
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not mark customer {CustomerId} as failed", customerId);
        }
    }
}

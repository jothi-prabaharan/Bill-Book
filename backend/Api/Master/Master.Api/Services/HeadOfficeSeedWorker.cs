using Master.Repository;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Services;

/// <summary>
/// Seeds the bootstrap head office (the Internal customer's branch) through
/// every service's own <c>internal/seed/organization</c>, once the host has
/// started (TK-208).
///
/// <c>DatabaseMigrationService</c> writes a few of that branch's rows directly,
/// because it runs before any service listens, and nothing ever ran the
/// services' own seeds for it. So the head office had no GST sub-accounts, and
/// its rates kept group 0 and collapsed to one choice on a document. The
/// other services' seeds are what repair and complete it.
///
/// Like <see cref="SampleDataBootstrapWorker"/>, it runs after start because
/// the services come up alongside Master, and a run that finds some of them
/// down is repeated a minute later, up to ten times. Every seed is idempotent,
/// so a run on each start only adds what is missing.
/// </summary>
public sealed class HeadOfficeSeedWorker : BackgroundService
{
    private const int Attempts = 10;
    private static readonly TimeSpan Pause = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<HeadOfficeSeedWorker> _log;

    public HeadOfficeSeedWorker(IServiceScopeFactory scopes, ILogger<HeadOfficeSeedWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (int attempt = 1; attempt <= Attempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(Pause, stoppingToken);

                using IServiceScope scope = _scopes.CreateScope();
                AdminDbContext db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

                bool exists = await db.Organizations.AnyAsync(
                    o => o.OrgId == InternalCustomerRepository.OrgId
                        && o.CustomerId == InternalCustomerRepository.CustomerId,
                    stoppingToken);

                if (!exists)
                {
                    // No bootstrap account on this install: nothing to seed.
                    return;
                }

                IReadOnlyList<string> unseeded = await scope.ServiceProvider
                    .GetRequiredService<ITenantSeeder>()
                    .SeedAsync(InternalCustomerRepository.CustomerId, InternalCustomerRepository.OrgId, stoppingToken);

                if (unseeded.Count == 0)
                {
                    _log.LogInformation("The head office branch is seeded in every service.");
                    return;
                }

                _log.LogInformation(
                    "Head office still unseeded after attempt {Attempt}: {Services}.",
                    attempt, string.Join(", ", unseeded));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Seeding the head office failed on attempt {Attempt}.", attempt);
            }
        }
    }
}

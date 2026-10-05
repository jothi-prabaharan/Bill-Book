using Master.Repository.Repositories;

namespace Master.Api.Services;

/// <summary>
/// Creates the demo customers on start when, and only when,
/// <c>Bootstrap:SeedDemoCustomers</c> is true (TK-154). Off by default, so a
/// production install never gets them by accident.
///
/// It runs after the host has started, because seeding a branch calls every
/// other service and those come up alongside this one; a run that finds some
/// of them down is repeated a minute later, up to ten times. Each run is
/// idempotent, so a repeat only finishes what the last one could not.
/// </summary>
public sealed class DemoCustomerBootstrapWorker : BackgroundService
{
    private const int Attempts = 10;
    private static readonly TimeSpan Pause = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<DemoCustomerBootstrapWorker> _log;

    public DemoCustomerBootstrapWorker(
        IServiceScopeFactory scopes, IConfiguration config, ILogger<DemoCustomerBootstrapWorker> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    /// <summary>
    /// The mailbox the demo owners' sign-ins are made from: the one asked
    /// for, else <c>Bootstrap:DemoOwnerEmail</c>, else <c>Bootstrap:OwnerEmail</c>.
    /// Null when none is an address.
    /// </summary>
    public static string? Mailbox(string? requested, IConfiguration config)
    {
        foreach (string? candidate in new[] { requested, config["Bootstrap:DemoOwnerEmail"], config["Bootstrap:OwnerEmail"] })
        {
            if (DemoCustomerRepository.OwnerEmail(candidate, Shared.Kernel.Apps.BusinessType.Retail) is not null)
            {
                return candidate!.Trim();
            }
        }

        return null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue<bool>("Bootstrap:SeedDemoCustomers"))
        {
            return;
        }

        if (Mailbox(null, _config) is not string mailbox)
        {
            _log.LogWarning("Bootstrap:SeedDemoCustomers is on but there is no Bootstrap:DemoOwnerEmail or OwnerEmail, so no demo customers were made.");
            return;
        }

        for (int attempt = 1; attempt <= Attempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(Pause, stoppingToken);

                using IServiceScope scope = _scopes.CreateScope();
                IReadOnlyList<DemoCustomerResult> results = await scope.ServiceProvider
                    .GetRequiredService<DemoCustomerRepository>()
                    .EnsureAllAsync(mailbox, stoppingToken);

                List<DemoCustomerResult> unfinished = results.Where(r => r.Unseeded.Count > 0).ToList();
                if (unfinished.Count == 0)
                {
                    _log.LogInformation("The demo customers are ready.");
                    return;
                }

                _log.LogInformation(
                    "Demo customers still unseeded after attempt {Attempt}: {Unfinished}.",
                    attempt, string.Join(", ", unfinished.Select(r => $"{r.CustomerCode} ({string.Join("/", r.Unseeded)})")));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Making the demo customers failed on attempt {Attempt}.", attempt);
            }
        }
    }
}

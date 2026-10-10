using Master.Repository.Repositories;

namespace Master.Api.Services;

/// <summary>
/// Creates the sample data on start when, and only when,
/// <c>Bootstrap:SeedSampleData</c> is true (TK-185): a sample customer with one
/// branch per business type, and a sample user who is Owner in all of them. Off
/// by default, so a production install never gets it by accident.
///
/// The user comes from <c>Bootstrap:SampleUserEmail</c>,
/// <c>Bootstrap:SampleUserName</c> and <c>Bootstrap:SampleUserPassword</c>; no
/// credential is in source. Without a password the user is created with none and
/// signs in through forgot-password.
///
/// It runs after the host has started, because seeding a branch calls every
/// other service and those come up alongside this one; a run that finds some of
/// them down is repeated a minute later, up to ten times. Each run is
/// idempotent, so a repeat only finishes what the last one could not.
/// </summary>
public sealed class SampleDataBootstrapWorker : BackgroundService
{
    private const int Attempts = 10;
    private static readonly TimeSpan Pause = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<SampleDataBootstrapWorker> _log;

    public SampleDataBootstrapWorker(
        IServiceScopeFactory scopes, IConfiguration config, ILogger<SampleDataBootstrapWorker> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue<bool>("Bootstrap:SeedSampleData"))
        {
            return;
        }

        SampleUser? user = SampleDataRepository.UserFrom(
            _config["Bootstrap:SampleUserEmail"], _config["Bootstrap:SampleUserName"], _config["Bootstrap:SampleUserPassword"]);

        if (user is null)
        {
            _log.LogWarning("Bootstrap:SeedSampleData is on but Bootstrap:SampleUserEmail is not an email address, so no sample data was made.");
            return;
        }

        for (int attempt = 1; attempt <= Attempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(Pause, stoppingToken);

                using IServiceScope scope = _scopes.CreateScope();
                SampleDataResult result = await scope.ServiceProvider
                    .GetRequiredService<SampleDataRepository>()
                    .EnsureAsync(user, stoppingToken);

                if (result.UserCreated)
                {
                    // The address, never a credential.
                    _log.LogInformation(
                        "Created the sample user {Email}{Password}.",
                        user.Email, user.Password is null ? ", with no password: use forgot-password to set one" : string.Empty);
                }

                List<SampleBranchResult> unfinished = result.Branches.Where(b => b.Unseeded.Count > 0).ToList();
                if (unfinished.Count == 0)
                {
                    _log.LogInformation(
                        "The sample data is ready: {Count} branches ({Codes}) for {Email}.",
                        result.Branches.Count, string.Join(", ", result.Branches.Select(b => b.OrgCode)), user.Email);
                    return;
                }

                _log.LogInformation(
                    "Sample branches still unseeded after attempt {Attempt}: {Unfinished}.",
                    attempt, string.Join(", ", unfinished.Select(b => $"{b.OrgCode} ({string.Join("/", b.Unseeded)})")));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Making the sample data failed on attempt {Attempt}.", attempt);
            }
        }
    }
}

using Claims.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

namespace Claims.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Claims context.
/// </summary>
public class DatabaseMigrationService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(IServiceProvider services, ILogger<DatabaseMigrationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting automated database setup and migrations for Claims...");

        using IServiceScope scope = _services.CreateScope();
        ClaimsDbContext db = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Claims", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


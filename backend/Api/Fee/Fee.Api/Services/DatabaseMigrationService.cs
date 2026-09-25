using Microsoft.EntityFrameworkCore;
using Npgsql;
using Fee.Repository;

namespace Fee.Api.Services;

/// <summary>
/// Runs the fee migrations on startup. Master migrates every tenant schema
/// too, into every shard; this covers the service started on its own.
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
        using IServiceScope scope = _services.CreateScope();
        FeeDbContext db = scope.ServiceProvider.GetRequiredService<FeeDbContext>();

        int retries = 5;
        while (retries > 0)
        {
            try
            {
                _logger.LogInformation("Migrating Fee database...");
                await db.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Fee database migration complete.");
                break;
            }
            catch (PostgresException ex) when (ex.SqlState == "3D000") // invalid_catalog_name
            {
                retries--;
                if (retries == 0) throw;
                _logger.LogWarning("Database does not exist yet. Waiting 2s for Master.Api to create it...");
                await Task.Delay(2000, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using Customer.Repository;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Customer.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Inventory context.
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
        _logger.LogInformation("Starting automated database setup and migrations for Inventory...");

        using var scope = _services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        
        int retries = 5;
        while (retries > 0)
        {
            try
            {
                _logger.LogInformation("Migrating Inventory database...");
                await db.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Inventory database migration complete.");
                break;
            }
            catch (Exception ex) when (ex is PostgresException { SqlState: "3D000" } || ex is NpgsqlException || ex is TimeoutException || ex.InnerException is TimeoutException)
            {
                retries--;
                if (retries == 0) throw;

                if (ex is PostgresException { SqlState: "3D000" })
                    _logger.LogWarning("Database does not exist yet. Waiting 2s for Master.Api to create it...");
                else
                    _logger.LogWarning("Transient error ({Message}) during migration. Retrying in 2s...", ex.Message);

                // Add jitter to avoid thundering herd when 20 services start at once
                await Task.Delay(2000 + Random.Shared.Next(0, 1000), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


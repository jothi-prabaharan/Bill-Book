using TimeLeave.Repository.Repositories;
using TimeLeave.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeLeave.Repository;

namespace TimeLeave.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the TimeLeave context.
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
        _logger.LogInformation("Starting automated database setup and migrations for TimeLeave...");

        using IServiceScope scope = _services.CreateScope();

        TimeLeaveDbContext db = scope.ServiceProvider.GetRequiredService<TimeLeaveDbContext>();

        int retries = 5;
        while (retries > 0)
        {
            try
            {
                _logger.LogInformation("Migrating TimeLeave database...");
                await db.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("TimeLeave database migration complete.");
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


using Student.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Student.Repository;

namespace Student.Api.Services;

/// <summary>
/// Runs the sis migrations on startup. Master migrates every tenant schema
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
        StudentDbContext db = scope.ServiceProvider.GetRequiredService<StudentDbContext>();

        int retries = 5;
        while (retries > 0)
        {
            try
            {
                _logger.LogInformation("Migrating Student database...");
                await db.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Student database migration complete.");
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


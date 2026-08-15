using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Accounting.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Accounting context.
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
        _logger.LogInformation("Starting automated database setup and migrations for Accounting...");

        using var scope = _services.CreateScope();

        var accountingDb = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
        
        // Wait and retry if the database itself hasn't been created by Master.Api yet
        int retries = 5;
        while (retries > 0)
        {
            try
            {
                _logger.LogInformation("Migrating Accounting database...");
                await accountingDb.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Accounting database migration complete.");
                break;
            }
            catch (PostgresException ex) when (ex.SqlState == "3D000") // 3D000 is invalid_catalog_name (db does not exist)
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

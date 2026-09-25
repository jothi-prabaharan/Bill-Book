using Microsoft.EntityFrameworkCore;
using Npgsql;
using Payroll.Repository;

namespace Payroll.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Employee context.
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
        _logger.LogInformation("Starting automated database setup and migrations for Employee...");

        using IServiceScope scope = _services.CreateScope();

        PayrollDbContext db = scope.ServiceProvider.GetRequiredService<PayrollDbContext>();

        int retries = 5;
        while (retries > 0)
        {
            try
            {
                _logger.LogInformation("Migrating Employee database...");
                await db.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Employee database migration complete.");
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

using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Employee.Repository;

namespace Employee.Api.Services;

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

        EmployeeDbContext db = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Employee", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


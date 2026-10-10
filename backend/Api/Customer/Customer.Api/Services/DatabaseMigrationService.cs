using Customer.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

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

        await StartupMigration.MigrateAsync(db.Database, _logger, "Customer", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using MaintenanceContract.Repository;

namespace MaintenanceContract.Api.Services;

/// <summary>
/// Runs the amc migrations on startup. Master migrates every tenant schema
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
        MaintenanceContractDbContext db = scope.ServiceProvider.GetRequiredService<MaintenanceContractDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "MaintenanceContract", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


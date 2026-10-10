using WorkOrder.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using WorkOrder.Repository;

namespace WorkOrder.Api.Services;

/// <summary>
/// Runs the wrk migrations on startup. Master migrates every tenant schema
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
        WorkOrderDbContext db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "WorkOrder", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


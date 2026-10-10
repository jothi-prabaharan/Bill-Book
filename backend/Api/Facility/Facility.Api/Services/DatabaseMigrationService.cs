using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Facility.Repository;

namespace Facility.Api.Services;

/// <summary>
/// Runs the fac migrations on startup. Master migrates every tenant schema
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
        FacilityDbContext db = scope.ServiceProvider.GetRequiredService<FacilityDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Facility", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


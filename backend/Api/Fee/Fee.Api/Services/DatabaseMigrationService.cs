using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Fee.Repository;

namespace Fee.Api.Services;

/// <summary>
/// Runs the fee migrations on startup. Master migrates every tenant schema
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
        FeeDbContext db = scope.ServiceProvider.GetRequiredService<FeeDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Fee", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


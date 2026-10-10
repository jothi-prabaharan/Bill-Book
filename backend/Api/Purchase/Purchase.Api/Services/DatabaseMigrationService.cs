using Purchase.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

namespace Purchase.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Purchase context.
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
        _logger.LogInformation("Starting automated database setup and migrations for Purchase...");

        using var scope = _services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<PurchaseDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Purchase", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


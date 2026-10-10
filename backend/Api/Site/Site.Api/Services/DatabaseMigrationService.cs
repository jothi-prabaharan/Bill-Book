using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Site.Repository;

namespace Site.Api.Services;

/// <summary>
/// Runs the sit migrations on startup (TK-169).
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
        SiteDbContext db = scope.ServiceProvider.GetRequiredService<SiteDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Site", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


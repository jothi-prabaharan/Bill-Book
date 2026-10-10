using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Performance.Repository;

namespace Performance.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Performance context.
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
        _logger.LogInformation("Starting automated database setup and migrations for Performance...");

        using IServiceScope scope = _services.CreateScope();
        PerformanceDbContext db = scope.ServiceProvider.GetRequiredService<PerformanceDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Performance", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


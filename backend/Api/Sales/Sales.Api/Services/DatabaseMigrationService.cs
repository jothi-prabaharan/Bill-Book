using Sales.Repository.Repositories;
using Sales.Repository.Clients;
using Sales.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

namespace Sales.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Sales context.
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
        _logger.LogInformation("Starting automated database setup and migrations for Sales...");

        using var scope = _services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Sales", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


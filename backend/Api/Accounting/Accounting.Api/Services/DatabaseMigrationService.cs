using Accounting.Repository.Repositories;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

namespace Accounting.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Accounting context.
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
        _logger.LogInformation("Starting automated database setup and migrations for Accounting...");

        using var scope = _services.CreateScope();

        var accountingDb = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        await StartupMigration.MigrateAsync(accountingDb.Database, _logger, "Accounting", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


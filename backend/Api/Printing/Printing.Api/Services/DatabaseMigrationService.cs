using Printing.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Printing.Repository;

namespace Printing.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Printing context.
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
        _logger.LogInformation("Starting automated database setup and migrations for Printing...");

        using IServiceScope scope = _services.CreateScope();

        PrintingDbContext db = scope.ServiceProvider.GetRequiredService<PrintingDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Printing", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


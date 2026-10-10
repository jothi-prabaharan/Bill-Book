using TimeLeave.Repository.Repositories;
using TimeLeave.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using TimeLeave.Repository;

namespace TimeLeave.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the TimeLeave context.
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
        _logger.LogInformation("Starting automated database setup and migrations for TimeLeave...");

        using IServiceScope scope = _services.CreateScope();

        TimeLeaveDbContext db = scope.ServiceProvider.GetRequiredService<TimeLeaveDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "TimeLeave", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


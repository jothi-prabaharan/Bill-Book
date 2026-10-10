using Recruitment.Repository.Repositories;
using Recruitment.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Recruitment.Repository;

namespace Recruitment.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Recruitment context.
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
        _logger.LogInformation("Starting automated database setup and migrations for Recruitment...");

        using IServiceScope scope = _services.CreateScope();
        RecruitmentDbContext db = scope.ServiceProvider.GetRequiredService<RecruitmentDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Recruitment", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Payroll.Repository;

namespace Payroll.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup for the Employee context.
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
        _logger.LogInformation("Starting automated database setup and migrations for Employee...");

        using IServiceScope scope = _services.CreateScope();

        PayrollDbContext db = scope.ServiceProvider.GetRequiredService<PayrollDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _logger, "Payroll", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


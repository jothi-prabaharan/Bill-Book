using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Persistence;

namespace BillBook.Worker.Notification.Persistence;

/// <summary>
/// Migrates <c>ntf</c> at startup, before anything reads a message — the same
/// thing every service does for its own schema. Registered first, so it runs
/// before the consumer starts.
/// </summary>
public sealed class NotificationMigrationService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificationMigrationService> _log;

    public NotificationMigrationService(IServiceScopeFactory scopes, ILogger<NotificationMigrationService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        await StartupMigration.MigrateAsync(db.Database, _log, "ntf", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


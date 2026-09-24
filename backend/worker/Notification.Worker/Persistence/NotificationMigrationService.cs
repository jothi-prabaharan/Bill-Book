using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Notification.Worker.Persistence;

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

        _log.LogInformation("Migrating the ntf schema...");
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

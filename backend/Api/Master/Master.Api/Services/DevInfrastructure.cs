using System.Collections.Concurrent;
using System.Security.Claims;
using Shared.Kernel.Interfaces;

namespace Master.Api.Services;

/// <summary>
/// Development stand-ins so DI resolves and the flows run end to end locally.
/// Replace with Key Vault / Service Bus implementations for production —
/// neither of these is durable.
/// </summary>
///
/// <remarks>
/// <para>
/// This service both writes secrets and reads them, and it did not before the
/// merge. Provisioning wrote a customer's connection string here — Platform's
/// half — while opening a customer's database read one — Contacts' half. Each
/// half registered its own stand-in and neither could do the other's job:
/// Platform's kept secrets in a dictionary and had nothing to fall back on, and
/// Contacts' read configuration and threw on every write.
/// </para>
/// <para>
/// So: written secrets are remembered, and a name never written falls through to
/// configuration under <c>Secrets:</c>, then to the <c>TenantFallback</c>
/// connection string. That is what makes a developer's two flows work in one
/// process — provision a customer and it is readable, point at a database
/// somebody else provisioned and it is readable too.
/// </para>
/// <para>
/// <b>Still not a secret store.</b> The dictionary dies with the process, so a
/// customer provisioned before a restart is only reachable through the
/// configured fallback. Key Vault is the fix, and it is still to write.
/// </para>
/// </remarks>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new();
    private readonly IConfiguration _configuration;

    public InMemorySecretStore(IConfiguration configuration) => _configuration = configuration;

    public Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_secrets.TryGetValue(name, out string? written))
        {
            return Task.FromResult(written);
        }

        string? configured = _configuration[$"Secrets:{name}"]
            ?? _configuration.GetConnectionString("TenantFallback");

        return configured is null
            ? throw new KeyNotFoundException($"Secret '{name}' not found.")
            : Task.FromResult(configured);
    }

    public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        _secrets[name] = value;
        return Task.CompletedTask;
    }
}

public sealed class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) => _logger = logger;

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        _logger.LogInformation("EVENT (not delivered) {EventType}: {@Event}", typeof(TEvent).Name, @event);
        return Task.CompletedTask;
    }
}

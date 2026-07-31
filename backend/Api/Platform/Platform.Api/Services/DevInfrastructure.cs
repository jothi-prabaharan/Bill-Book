using System.Collections.Concurrent;
using System.Security.Claims;
using Shared.Kernel.Interfaces;

namespace Platform.Api.Services;

/// <summary>
/// Development stand-ins so DI resolves and the flows run end to end locally.
/// Replace with Key Vault / Service Bus implementations for production —
/// neither of these is durable.
/// </summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new();

    public Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default) =>
        _secrets.TryGetValue(name, out string? value)
            ? Task.FromResult(value)
            : throw new KeyNotFoundException($"Secret '{name}' not found.");

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

/// <summary>Resolves the caller from JWT claims; null for anonymous (signup) and system writes.</summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid? UserId => Claim(ClaimTypes.NameIdentifier) ?? Claim("sub");

    public Guid? CustomerId => Claim("customer_id");

    public Guid? OrgId => Claim("org_id");

    private Guid? Claim(string type)
    {
        string? value = _accessor.HttpContext?.User.FindFirst(type)?.Value;
        return Guid.TryParse(value, out Guid id) ? id : null;
    }
}

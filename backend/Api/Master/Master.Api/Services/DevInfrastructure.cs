using System.Security.Claims;
using Shared.Kernel.Interfaces;

namespace Master.Api.Services;

/// <summary>
/// The one development stand-in left.
///
/// <b>The secret store that used to live here is gone.</b> It kept written
/// secrets in a dictionary that died with the process, so a customer
/// provisioned before a restart was only reachable through the configured
/// fallback — which made it worse than nothing, because it accepted every write
/// and reported success. <c>Shared.Kernel.Secrets</c> now holds a real Key
/// Vault-backed store and a configuration one, and refuses to start a
/// production deployment that has neither.
///
/// <see cref="LoggingEventPublisher"/> is still a stand-in and still logs
/// rather than delivering, which is why nothing that reads an event works yet:
/// nothing publishes one anywhere it can be read. Service Bus is the fix and it
/// is still to write.
/// </summary>
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

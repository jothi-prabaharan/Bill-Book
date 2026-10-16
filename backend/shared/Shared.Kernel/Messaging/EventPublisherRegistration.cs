using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Interfaces;

namespace Shared.Kernel.Messaging;

/// <summary>
/// Logs events cleanly.
/// </summary>
public sealed class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) => _logger = logger;

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        _logger.LogInformation(
            "EVENT {EventType}: {@Event}", typeof(TEvent).Name, @event);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Registers the event publisher.
/// </summary>
public static class EventPublisherRegistration
{
    public static IServiceCollection AddEventPublisher(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IEventPublisher, LoggingEventPublisher>();
        return services;
    }
}

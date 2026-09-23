using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Interfaces;

namespace Shared.Kernel.Messaging;

/// <summary>
/// Logs and delivers nothing.
///
/// <b>Development only, and it is the reason nothing that reads an event works
/// in a local run.</b> It used to live in <c>Master.Api</c> as the last of that
/// service's development stand-ins; it moved here so the choice between it and
/// a real publisher is made in one place, the way the secret store's and the
/// file store's already are.
/// </summary>
public sealed class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) => _logger = logger;

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        _logger.LogInformation(
            "EVENT (not delivered) {EventType}: {@Event}", typeof(TEvent).Name, @event);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Chooses the event publisher, the way
/// <see cref="Secrets.SecretStoreRegistration"/> and
/// <see cref="Storage.FileStorageRegistration"/> choose theirs.
/// </summary>
public static class EventPublisherRegistration
{
    /// <summary>
    /// Service Bus when <c>ServiceBus:Namespace</c> is set — the fully qualified
    /// host, <c>&lt;name&gt;.servicebus.windows.net</c> — the logging stand-in
    /// otherwise.
    ///
    /// <b>A namespace, never a connection string.</b> A Service Bus connection
    /// string carries a shared access key, and a key in configuration is the
    /// problem the managed identity exists to remove. The identity needs Azure
    /// Service Bus Data Sender on the namespace; deploy/azure grants it.
    ///
    /// <b>No Production guard.</b> An event that is logged rather than sent is
    /// a feature that does not run — the stand-in says so on every publish,
    /// with "not delivered" in the line — but not worth refusing to serve the
    /// requests that do not depend on it.
    /// </summary>
    public static IServiceCollection AddEventPublisher(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration["ServiceBus:Namespace"] is { Length: > 0 } ns)
        {
            string? prefix = configuration["ServiceBus:TopicPrefix"];

            services.AddSingleton<IEventPublisher>(_ => new ServiceBusEventPublisher(
                new ServiceBusClient(ns, new DefaultAzureCredential()), prefix));

            return services;
        }

        services.AddSingleton<IEventPublisher, LoggingEventPublisher>();

        return services;
    }
}

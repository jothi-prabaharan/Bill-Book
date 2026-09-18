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
    /// Pub/Sub when <c>Gcp:ProjectId</c> is set, the logging stand-in otherwise.
    ///
    /// <b>There is no Service Bus branch, because there is no Service Bus
    /// implementation.</b> It was the intended answer for a long time and was
    /// never written; the package is still pinned. An Azure deployment therefore
    /// still publishes nothing, which is the state the whole product was in
    /// before this — worth saying plainly rather than leaving the shape of this
    /// method to imply parity it does not have.
    /// </summary>
    public static IServiceCollection AddEventPublisher(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration["Gcp:ProjectId"] is { Length: > 0 } project)
        {
            string? prefix = configuration["Gcp:PubSub:TopicPrefix"];

            // Defaults on: a topic missing because nobody declared it should not
            // be discovered at the moment an event is first published.
            bool create = configuration.GetValue("Gcp:PubSub:CreateMissingTopics", true);

            services.AddSingleton<IEventPublisher>(
                _ => new PubSubEventPublisher(project, prefix, create));

            return services;
        }

        services.AddSingleton<IEventPublisher, LoggingEventPublisher>();

        return services;
    }
}

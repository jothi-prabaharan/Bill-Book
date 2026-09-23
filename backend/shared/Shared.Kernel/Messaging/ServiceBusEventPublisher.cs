using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Shared.Kernel.Interfaces;

namespace Shared.Kernel.Messaging;

/// <summary>
/// Azure Service Bus, for deployments.
///
/// <b>This is the first <c>IEventPublisher</c> that delivers anything on
/// Azure.</b> Service Bus was the intended answer for as long as the interface
/// has existed and was never written — the package sat pinned and unused — so
/// every Azure-shaped deployment logged its events and dropped them, and nothing
/// that reads an event has ever run.
///
/// <b>One topic per event type.</b> A single topic carrying everything would
/// make every subscriber receive and discard most of what it got, or else carry
/// a filter rule that has to be kept in step with the publisher by hand.
///
/// <b>Delivery is at-least-once, so every consumer must dedupe on
/// <see cref="ServiceBusMessage.MessageId"/>.</b> The topics deploy/azure
/// declares have duplicate detection on, which drops a second send of the same
/// id at the broker — covering the case where this side times out, retries, and
/// the first attempt had in fact landed. It does nothing for redelivery to a
/// consumer that failed to complete a message, which is the case the interface
/// has always warned about.
///
/// <b>Topics are not created here.</b> The identity a deployed service runs as
/// holds Data Sender, which can send and cannot manage — deliberately, since a
/// service able to create entities can also delete them. A missing topic is an
/// infrastructure omission and fails loudly, naming the topic and where to
/// declare it.
/// </summary>
public sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    /// <summary>
    /// Web defaults — camelCase — the same wire shape every other JSON payload
    /// in this system uses, so a consumer can read an event with the options it
    /// already has.
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ServiceBusClient _client;
    private readonly string _topicPrefix;

    /// <summary>
    /// Senders are thread-safe and hold a link to their topic, so one per topic
    /// is kept for the life of the process rather than opened per event.
    /// </summary>
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders =
        new(StringComparer.Ordinal);

    public ServiceBusEventPublisher(ServiceBusClient client, string? topicPrefix = null)
    {
        _client = client;
        _topicPrefix = topicPrefix ?? string.Empty;
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        string topic = TopicFor(typeof(TEvent).Name, _topicPrefix);

        ServiceBusSender sender = _senders.GetOrAdd(topic, _client.CreateSender);

        try
        {
            await sender.SendMessageAsync(BuildMessage(@event), cancellationToken);
        }
        catch (ServiceBusException ex)
            when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            throw new InvalidOperationException(
                $"Service Bus topic '{topic}' does not exist. Declare it in the topics list "
                + "in deploy/azure/main.bicep; deployed services cannot create topics.",
                ex);
        }
    }

    /// <summary>
    /// Topic name for an event type, prefixed when two environments share a
    /// namespace. Public so the naming rule is tested directly rather than
    /// inferred from a send.
    /// </summary>
    public static string TopicFor(string eventTypeName, string? prefix) =>
        string.IsNullOrEmpty(prefix) ? eventTypeName : $"{prefix}-{eventTypeName}";

    /// <summary>
    /// The message for an event. Public for the same reason as
    /// <see cref="TopicFor"/>: its shape is the contract with every consumer,
    /// and a consumer written against it should not have to discover it from a
    /// live broker.
    /// </summary>
    public static ServiceBusMessage BuildMessage<TEvent>(TEvent @event)
        where TEvent : class
    {
        string eventType = typeof(TEvent).Name;

        return new ServiceBusMessage(BinaryData.FromString(JsonSerializer.Serialize(@event, Json)))
        {
            // The dedupe key, both for the broker's duplicate detection and for
            // consumers. Generated here because no event type in this system
            // carries its own id yet, and the guarantee is needed regardless.
            MessageId = Guid.NewGuid().ToString("N"),
            ContentType = "application/json",
            // Service Bus's own routing field, so a subscription can filter on it
            // without a custom property and a dead-lettered message says what it
            // was in the portal's list view.
            Subject = eventType,
            ApplicationProperties =
            {
                ["eventType"] = eventType,
                ["publishedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            },
        };
    }

    /// <summary>
    /// Closes every sender's link and then the connection. Sends are not
    /// batched, so there is nothing queued to flush — unlike a publisher that
    /// buffers, a shutdown here loses nothing that a caller was told succeeded.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (ServiceBusSender sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }

        _senders.Clear();

        await _client.DisposeAsync();
    }
}

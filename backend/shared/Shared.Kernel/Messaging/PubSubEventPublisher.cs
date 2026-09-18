using System.Collections.Concurrent;
using System.Text.Json;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;

namespace Shared.Kernel.Messaging;

/// <summary>
/// Pub/Sub, for deployments on Google Cloud.
///
/// <b>This is the first <c>IEventPublisher</c> that delivers anything.</b>
/// Everything before it logged and dropped, which is why nothing that reads an
/// event has ever worked — nothing published one anywhere it could be read.
/// Service Bus was the intended answer and was never written; Pub/Sub arrived
/// first because this deployment target needed one, and the interface does not
/// care which.
///
/// <b>Delivery is at-least-once, so every consumer must dedupe on the event
/// id.</b> That is not a caveat about Pub/Sub in particular — Service Bus is the
/// same, and the interface has said so since before either existed. The id is
/// published as the <c>eventId</c> attribute precisely so a consumer has
/// something to dedupe on that does not depend on the payload's shape.
///
/// <b>One topic per event type.</b> A single topic carrying every event would
/// make every consumer read and discard most of what it received, and Pub/Sub
/// filters are a per-subscription property that would then have to be kept in
/// step with the publisher by hand.
/// </summary>
public sealed class PubSubEventPublisher : IAsyncDisposable, Interfaces.IEventPublisher
{
    /// <summary>
    /// Payloads go out as JSON with the same options everywhere, so a consumer
    /// written against one publisher's output can read another's. Web defaults
    /// mean camelCase, which is what every other wire format in this system uses.
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _projectId;
    private readonly string _topicPrefix;
    private readonly bool _createMissingTopics;

    /// <summary>
    /// A <see cref="PublisherClient"/> owns gRPC channels and a batching queue,
    /// so it is built once per topic and kept. <see cref="Lazy{T}"/> around the
    /// task means two concurrent publishes of the same event type build one
    /// client rather than two, and the loser of the race does not leak the one
    /// it made.
    /// </summary>
    private readonly ConcurrentDictionary<string, Lazy<Task<PublisherClient>>> _publishers =
        new(StringComparer.Ordinal);

    public PubSubEventPublisher(
        string projectId, string? topicPrefix = null, bool createMissingTopics = true)
    {
        _projectId = projectId;
        _topicPrefix = topicPrefix ?? string.Empty;
        _createMissingTopics = createMissingTopics;
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        TopicName topic = TopicNameFor(typeof(TEvent).Name);

        PublisherClient publisher = await GetPublisherAsync(topic, cancellationToken);

        var message = new PubsubMessage
        {
            Data = ByteString.CopyFromUtf8(JsonSerializer.Serialize(@event, Json)),
            Attributes =
            {
                // The dedupe key. Generated here rather than taken from the
                // event because no event type in this system carries one yet,
                // and a consumer needs the guarantee regardless of which does.
                ["eventId"] = Guid.NewGuid().ToString("N"),
                // So a consumer can route without deserializing first, and so a
                // dead-letter message says what it was.
                ["eventType"] = typeof(TEvent).Name,
                ["publishedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            },
        };

        await publisher.PublishAsync(message);
    }

    /// <summary>
    /// Topics are named after the event type, optionally prefixed so two
    /// deployments can share a project without colliding — which is what a
    /// staging environment alongside production usually means.
    /// </summary>
    private TopicName TopicNameFor(string eventTypeName) =>
        new(_projectId, _topicPrefix.Length > 0 ? $"{_topicPrefix}-{eventTypeName}" : eventTypeName);

    private Task<PublisherClient> GetPublisherAsync(
        TopicName topic, CancellationToken cancellationToken) =>
        _publishers.GetOrAdd(
            topic.TopicId,
            _ => new Lazy<Task<PublisherClient>>(() => CreatePublisherAsync(topic, cancellationToken)))
            .Value;

    /// <summary>
    /// Creates the topic when it is missing and <c>createMissingTopics</c> is on.
    ///
    /// The Terraform under <c>deploy/gcp</c> declares the topics, so in a
    /// provisioned environment this never fires. It exists for the case that
    /// bites otherwise: a new event type added in code, deployed before anyone
    /// remembered the topic, failing at the moment it is first published rather
    /// than at deployment. Turning it off (<c>Gcp:PubSub:CreateMissingTopics</c>)
    /// is the right choice where topic creation is an infrastructure decision
    /// and an unexpected topic should be a visible failure.
    /// </summary>
    private async Task<PublisherClient> CreatePublisherAsync(
        TopicName topic, CancellationToken cancellationToken)
    {
        if (_createMissingTopics)
        {
            PublisherServiceApiClient api = await PublisherServiceApiClient.CreateAsync(cancellationToken);

            try
            {
                await api.CreateTopicAsync(topic, cancellationToken);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
            {
                // The ordinary case, and the case where another replica won the
                // race. Both mean the topic is there.
            }
        }

        return await PublisherClient.CreateAsync(topic);
    }

    /// <summary>
    /// Flushes what is batched and not yet sent. Without this a shutdown drops
    /// whatever was queued in the last few milliseconds, which is the kind of
    /// loss that shows up as one missing ledger posting a month later.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (Lazy<Task<PublisherClient>> entry in _publishers.Values)
        {
            if (!entry.IsValueCreated)
            {
                continue;
            }

            try
            {
                PublisherClient publisher = await entry.Value;
                await publisher.ShutdownAsync(CancellationToken.None);
            }
            catch (Exception ex) when (ex is RpcException or OperationCanceledException)
            {
                // Shutting down. A publisher that cannot be reached to flush is
                // not something a disposing process can do anything about, and
                // throwing here would mask whatever is actually stopping the host.
            }
        }

        _publishers.Clear();
    }
}

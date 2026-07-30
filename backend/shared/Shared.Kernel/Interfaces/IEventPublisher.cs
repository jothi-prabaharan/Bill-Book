namespace Shared.Kernel.Interfaces;

/// <summary>
/// Service Bus abstraction. Sales/Purchase/Banking publish integration events;
/// Accounting and the workers consume them. Delivery is at-least-once, so every
/// consumer must dedupe on the event id.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class;
}

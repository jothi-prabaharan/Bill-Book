namespace Shared.Kernel.Messaging;

/// <summary>
/// Published when an email requested via Notification.Worker is confirmed delivered.
/// </summary>
public sealed class EmailDelivered
{
    public required string MessageId { get; init; }
    public required DateTimeOffset DeliveredAt { get; init; }
    public Guid? CustomerId { get; init; }
    public required string ToEmail { get; init; }
}

/// <summary>
/// Published when an email requested via Notification.Worker fails delivery or bounces.
/// </summary>
public sealed class EmailFailed
{
    public required string MessageId { get; init; }
    public required DateTimeOffset FailedAt { get; init; }
    public Guid? CustomerId { get; init; }
    public required string ToEmail { get; init; }
    public string? Reason { get; init; }
    public bool IsHardBounce { get; init; }
}

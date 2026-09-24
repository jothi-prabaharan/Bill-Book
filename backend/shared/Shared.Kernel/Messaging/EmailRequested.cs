using Shared.Kernel.Interfaces;

namespace Shared.Kernel.Messaging;

/// <summary>
/// A request to send one email, published by Master and delivered by
/// <c>Notification.Worker</c> (TK-19). Its topic is <c>EmailRequested</c>.
///
/// <b><see cref="MessageId"/> is the dedupe key</b>, chosen once by the sender
/// and carried in the body rather than left to the transport: Service Bus
/// delivers at least once, and the worker records each id it has sent so a
/// redelivered request sends nothing. Every field of <see cref="EmailMessage"/>
/// travels with it, so the worker needs nothing else from the request.
/// </summary>
public sealed class EmailRequested
{
    public required string MessageId { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>Whose mailbox to send from. Null is the platform's own.</summary>
    public Guid? CustomerId { get; init; }

    public required string ToEmail { get; init; }

    public string? ToName { get; init; }

    public required string Subject { get; init; }

    public required string HtmlBody { get; init; }

    public string? TextBody { get; init; }

    public static EmailRequested From(EmailMessage message, string messageId, DateTimeOffset requestedAt) => new()
    {
        MessageId = messageId,
        RequestedAt = requestedAt,
        CustomerId = message.CustomerId,
        ToEmail = message.ToEmail,
        ToName = message.ToName,
        Subject = message.Subject,
        HtmlBody = message.HtmlBody,
        TextBody = message.TextBody,
    };

    public EmailMessage ToMessage() => new()
    {
        CustomerId = CustomerId,
        ToEmail = ToEmail,
        ToName = ToName,
        Subject = Subject,
        HtmlBody = HtmlBody,
        TextBody = TextBody,
    };
}

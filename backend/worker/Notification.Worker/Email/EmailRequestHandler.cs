using Microsoft.Extensions.Logging;
using Notification.Worker.Persistence;
using Shared.Kernel.Email;
using Shared.Kernel.Messaging;

namespace Notification.Worker.Email;

public enum EmailHandleOutcome
{
    /// <summary>Sent now. Complete the message.</summary>
    Sent,

    /// <summary>Sent before; this is a redelivery. Complete it and send nothing.</summary>
    Duplicate,

    /// <summary>Another delivery is sending it. Abandon it so it comes back later.</summary>
    InProgress,

    /// <summary>No mailbox to send from. Retrying cannot help; dead-letter it.</summary>
    NoMailbox,
}

/// <summary>
/// Sends one <see cref="EmailRequested"/>, once (TK-19): claim the message id,
/// resolve the customer's mailbox from Master, send, mark it sent. A failure
/// anywhere after the claim gives the claim back and rethrows, so the message
/// is abandoned and the next delivery sends it.
/// </summary>
public sealed class EmailRequestHandler
{
    public const string Topic = nameof(EmailRequested);

    private readonly IProcessedMessageStore _store;
    private readonly ISmtpDirectory _smtp;
    private readonly IMailTransport _transport;
    private readonly ILogger<EmailRequestHandler> _log;

    public EmailRequestHandler(
        IProcessedMessageStore store,
        ISmtpDirectory smtp,
        IMailTransport transport,
        ILogger<EmailRequestHandler> log)
    {
        _store = store;
        _smtp = smtp;
        _transport = transport;
        _log = log;
    }

    public async Task<EmailHandleOutcome> HandleAsync(EmailRequested request, CancellationToken ct)
    {
        switch (await _store.TryClaimAsync(request.MessageId, Topic, ct))
        {
            case ClaimOutcome.AlreadySent:
                _log.LogInformation("Message {MessageId} was already sent; completing the redelivery.", request.MessageId);
                return EmailHandleOutcome.Duplicate;

            case ClaimOutcome.InProgress:
                return EmailHandleOutcome.InProgress;
        }

        try
        {
            ResolvedSmtp? smtp = await _smtp.ResolveAsync(request.CustomerId, ct);

            if (smtp is null)
            {
                await _store.ReleaseAsync(request.MessageId, CancellationToken.None);
                return EmailHandleOutcome.NoMailbox;
            }

            await _transport.SendAsync(smtp, request.ToMessage(), ct);
        }
        catch
        {
            await _store.ReleaseAsync(request.MessageId, CancellationToken.None);
            throw;
        }

        await _store.MarkSentAsync(request.MessageId, CancellationToken.None);

        // Never the body: invitation links and one-time codes travel in it.
        _log.LogInformation("Sent '{Subject}' to {ToEmail} ({MessageId}).", request.Subject, request.ToEmail, request.MessageId);
        return EmailHandleOutcome.Sent;
    }
}

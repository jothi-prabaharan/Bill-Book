namespace Shared.Kernel.Interfaces;

/// <summary>
/// Outbound email. Backed by SMTP settings from <c>mst.SmtpSettings</c>, whose
/// password is AES-encrypted at rest and decrypted here before authenticating
/// to the mail server. Invitations, OTPs and password-reset mail go through this.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>A single outbound message. HTML body; plain-text fallback optional.</summary>
public sealed class EmailMessage
{
    public required string ToEmail { get; init; }

    public string? ToName { get; init; }

    public required string Subject { get; init; }

    public required string HtmlBody { get; init; }

    public string? TextBody { get; init; }

    /// <summary>Null uses the system default mailbox; set to send from a customer's own SMTP row.</summary>
    public Guid? CustomerId { get; init; }
}

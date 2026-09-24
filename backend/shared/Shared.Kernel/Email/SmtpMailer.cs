using System.Net;
using System.Net.Mail;
using Shared.Kernel.Interfaces;

namespace Shared.Kernel.Email;

/// <summary>
/// A mailbox to send from, with its password already decrypted — what
/// <c>mst.SmtpSettings</c> resolves to for one customer.
///
/// Shared because two processes send mail (TK-19): Master, in process, when no
/// worker is running, and <c>Notification.Worker</c>, which reads it from
/// Master's internal API. It is never stored anywhere but where Master holds it.
/// </summary>
public sealed class ResolvedSmtp
{
    public required string Host { get; init; }

    public required int Port { get; init; }

    public required bool UseSsl { get; init; }

    public required string FromEmail { get; init; }

    public required string FromName { get; init; }

    public required string Username { get; init; }

    public required string Password { get; init; }
}

/// <summary>
/// Puts one message on the wire through one mailbox. The only code in the
/// product that talks SMTP, so the in-process sender and the worker cannot
/// build a message two different ways.
/// </summary>
public static class SmtpMailer
{
    public static async Task SendAsync(ResolvedSmtp smtp, EmailMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseSsl,
            Credentials = new NetworkCredential(smtp.Username, smtp.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        using MailMessage mail = Build(smtp, message);

        await client.SendMailAsync(mail, ct);
    }

    /// <summary>The message as it goes out. Public so its shape is tested without a mail server.</summary>
    public static MailMessage Build(ResolvedSmtp smtp, EmailMessage message)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(smtp.FromEmail, smtp.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };

        mail.To.Add(new MailAddress(message.ToEmail, message.ToName ?? message.ToEmail));

        if (!string.IsNullOrEmpty(message.TextBody))
        {
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                message.TextBody, null, "text/plain"));
        }

        return mail;
    }
}

using System.Net;
using System.Net.Mail;
using Shared.Kernel.Interfaces;

namespace Platform.Api.Services;

/// <summary>
/// Sends mail using the credentials in plt.SmtpSettings. Lives in Platform
/// because Platform owns that table — the decrypted password never crosses a
/// service boundary. Other services ask Platform to send instead.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettingsService _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(SmtpSettingsService settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ResolvedSmtp? smtp = await _settings.ResolveAsync(message.CustomerId, cancellationToken);
        if (smtp is null)
        {
            // No mailbox configured — surface it rather than dropping the mail silently.
            throw new InvalidOperationException(
                "No active SMTP settings are configured. Set them in Settings → Email.");
        }

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseSsl,
            Credentials = new NetworkCredential(smtp.Username, smtp.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        using var mail = new MailMessage
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

        await client.SendMailAsync(mail, cancellationToken);

        // Never log the body — invite links and OTP codes travel in it.
        _logger.LogInformation(
            "Sent '{Subject}' to {ToEmail} via {Host}", message.Subject, message.ToEmail, smtp.Host);
    }
}

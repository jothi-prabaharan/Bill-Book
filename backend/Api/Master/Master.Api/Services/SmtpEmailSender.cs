using Shared.Kernel.Email;
using Shared.Kernel.Interfaces;

namespace Master.Api.Services;

/// <summary>
/// Sends mail using the credentials in mst.SmtpSettings, in process — the path
/// every mail takes when no notification worker is running (TK-19). The SMTP
/// conversation itself is <see cref="SmtpMailer"/>'s, shared with the worker.
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
            throw new SmtpConfigurationException(
                "No active SMTP settings are configured. Set them in Settings → Email.");
        }

        await SmtpMailer.SendAsync(smtp, message, cancellationToken);

        // Never log the body — invite links and OTP codes travel in it.
        _logger.LogInformation(
            "Sent '{Subject}' to {ToEmail} via {Host}", message.Subject, message.ToEmail, smtp.Host);
    }
}

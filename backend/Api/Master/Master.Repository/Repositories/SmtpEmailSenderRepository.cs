using Master.Repository.Clients;
using Shared.Kernel.Email;
using Shared.Kernel.Interfaces;

namespace Master.Repository.Repositories;

/// <summary>
/// Sends mail using the credentials in mst.SmtpSettings, in process — the path
/// every mail takes when no notification worker is running (TK-19). The SMTP
/// conversation itself is <see cref="SmtpMailer"/>'s, shared with the worker.
/// </summary>
public sealed class SmtpEmailSenderRepository : IEmailSender
{
    private readonly SmtpSettingsRepository _settings;
    private readonly ILogger<SmtpEmailSenderRepository> _logger;

    public SmtpEmailSenderRepository(SmtpSettingsRepository settings, ILogger<SmtpEmailSenderRepository> logger)
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
            throw new SmtpConfigurationExceptionRepository(
                "No active SMTP settings are configured. Set them in Settings → Email.");
        }

        await SmtpMailer.SendAsync(smtp, message, cancellationToken);

        // Never log the body — invite links and OTP codes travel in it.
        _logger.LogInformation(
            "Sent '{Subject}' to {ToEmail} via {Host}", message.Subject, message.ToEmail, smtp.Host);
    }
}

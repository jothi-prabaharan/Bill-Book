using Shared.Kernel.Interfaces;

namespace Identity.Api.Services;

/// <summary>
/// Placeholder email sender: logs instead of delivering, so the auth flows run
/// end to end in development without SMTP. The real sender lives in the
/// Notification worker, reads plt.SmtpSettings and decrypts the password.
/// Do NOT use in production — it does not send mail.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EMAIL (not sent) to {ToEmail} · subject {Subject} · body {TextBody}",
            message.ToEmail, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }
}

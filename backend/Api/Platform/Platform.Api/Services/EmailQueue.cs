using System.Threading.Channels;
using Shared.Kernel.Interfaces;

namespace Platform.Api.Services;

public interface IEmailQueue
{
    ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default);

    ValueTask<EmailMessage> DequeueAsync(CancellationToken ct);
}

/// <summary>
/// In-process channel queue so an SMTP round-trip never blocks a request.
/// Not durable: a restart loses anything still queued, which is acceptable for
/// invitations and OTPs because both can be re-requested. Swap for Service Bus
/// behind this interface when the Notification worker lands.
/// </summary>
public sealed class InProcessEmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(message, ct);

    public ValueTask<EmailMessage> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}

/// <summary>
/// The IEmailSender callers see: queues and returns immediately. Delivery
/// happens on the background worker.
/// </summary>
public sealed class QueuedEmailSender : IEmailSender
{
    private readonly IEmailQueue _queue;

    public QueuedEmailSender(IEmailQueue queue) => _queue = queue;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        await _queue.EnqueueAsync(message, cancellationToken);
}

/// <summary>
/// Drains the queue and delivers over SMTP, retrying transient failures with
/// backoff. A message that still fails is logged and dropped — the user can
/// resend the invitation or request a new code.
/// </summary>
public sealed class EmailDispatchWorker : BackgroundService
{
    private static readonly TimeSpan[] Backoff =
    {
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
    };

    private readonly IEmailQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<EmailDispatchWorker> _logger;

    public EmailDispatchWorker(
        IEmailQueue queue, IServiceScopeFactory scopes, ILogger<EmailDispatchWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            EmailMessage message;
            try
            {
                message = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await DeliverAsync(message, stoppingToken);
        }
    }

    private async Task DeliverAsync(EmailMessage message, CancellationToken ct)
    {
        for (int attempt = 0; attempt <= Backoff.Length; attempt++)
        {
            try
            {
                using IServiceScope scope = _scopes.CreateScope();
                SmtpEmailSender sender = scope.ServiceProvider.GetRequiredService<SmtpEmailSender>();
                await sender.SendAsync(message, ct);
                return;
            }
            catch (Exception ex) when (attempt < Backoff.Length)
            {
                _logger.LogWarning(
                    ex,
                    "Send to {ToEmail} failed (attempt {Attempt}); retrying in {Delay}",
                    message.ToEmail, attempt + 1, Backoff[attempt]);
                try
                {
                    await Task.Delay(Backoff[attempt], ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                // Never log the body — it carries invite links and OTP codes.
                _logger.LogError(
                    ex, "Giving up on '{Subject}' to {ToEmail}", message.Subject, message.ToEmail);
                return;
            }
        }
    }
}

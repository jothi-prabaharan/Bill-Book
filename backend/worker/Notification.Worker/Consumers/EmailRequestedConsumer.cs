using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification.Worker.Email;
using Shared.Kernel.Messaging;

namespace Notification.Worker.Consumers;

/// <summary>
/// Reads <c>EmailRequested</c> from Service Bus and hands each to
/// <see cref="EmailRequestHandler"/> (TK-19). Registered only when
/// <c>ServiceBus:Namespace</c> is set; without it Master sends in process and
/// there is nothing here to read.
///
/// Messages are settled by hand, never auto-completed: a message is completed
/// only once it has been sent or found already sent, abandoned when a send
/// fails or another delivery holds it — Service Bus then redelivers, and after
/// its maximum delivery count dead-letters it — and dead-lettered at once when
/// it is unreadable or has no mailbox to go through, since retrying cannot fix
/// either.
/// </summary>
public sealed class EmailRequestedConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ServiceBusClient _client;
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailRequestedConsumer> _log;

    public EmailRequestedConsumer(
        ServiceBusClient client,
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILogger<EmailRequestedConsumer> log)
    {
        _client = client;
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string topic = ServiceBusEventPublisher.TopicFor(nameof(EmailRequested), _config["ServiceBus:TopicPrefix"]);
        string subscription = _config["ServiceBus:EmailSubscription"] ?? "notification-worker";

        await using ServiceBusProcessor processor = _client.CreateProcessor(topic, subscription, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 4,
        });

        processor.ProcessMessageAsync += OnMessageAsync;
        processor.ProcessErrorAsync += args =>
        {
            _log.LogError(args.Exception, "Service Bus error on {Entity} ({Source}).", args.EntityPath, args.ErrorSource);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }

        await processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        EmailRequested? request = Read(args.Message);

        if (request is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "Unreadable", "The body is not an EmailRequested.");
            return;
        }

        try
        {
            using IServiceScope scope = _scopes.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<EmailRequestHandler>();

            switch (await handler.HandleAsync(request, args.CancellationToken))
            {
                case EmailHandleOutcome.Sent:
                case EmailHandleOutcome.Duplicate:
                    await args.CompleteMessageAsync(args.Message);
                    break;

                case EmailHandleOutcome.NoMailbox:
                    await args.DeadLetterMessageAsync(
                        args.Message, "NoMailbox", "No SMTP settings for this customer or the platform.");
                    break;

                default:
                    await args.AbandonMessageAsync(args.Message);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Sending {MessageId} failed; it will be delivered again.", request.MessageId);
            await args.AbandonMessageAsync(args.Message);
        }
    }

    /// <summary>
    /// The request in a message, or null when it is not one. A body with no
    /// message id of its own falls back to the transport's, which is still a
    /// stable key across redeliveries of that one message. Public for its tests.
    /// </summary>
    public static EmailRequested? Read(ServiceBusReceivedMessage message)
    {
        EmailRequested? request;

        try
        {
            request = message.Body.ToObjectFromJson<EmailRequested>(Json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ToEmail))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(request.MessageId)
            ? new EmailRequested
            {
                MessageId = message.MessageId,
                RequestedAt = request.RequestedAt,
                CustomerId = request.CustomerId,
                ToEmail = request.ToEmail,
                ToName = request.ToName,
                Subject = request.Subject,
                HtmlBody = request.HtmlBody,
                TextBody = request.TextBody,
            }
            : request;
    }
}

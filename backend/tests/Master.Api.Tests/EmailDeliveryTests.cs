using Master.Api.Services;
using Microsoft.Extensions.Configuration;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Messaging;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Which path an email takes (TK-19): an event for <c>Notification.Worker</c>
/// when a broker and a subscribed worker are both there, the in-process queue
/// otherwise — so a one-time code still goes out with Service Bus unset.
/// </summary>
public sealed class EmailDeliveryTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<object> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            Published.Add(@event);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void With_service_bus_unset_mail_stays_in_process()
    {
        Assert.False(EmailDelivery.UseWorker(Config()));
        Assert.False(EmailDelivery.UseWorker(Config(("Notification:EmailWorker", "true"))));
    }

    [Fact]
    public void A_namespace_alone_does_not_switch_mail_to_a_worker_that_may_not_be_running()
    {
        Assert.False(EmailDelivery.UseWorker(Config(("ServiceBus:Namespace", "sb-test.servicebus.windows.net"))));
    }

    [Fact]
    public void A_namespace_and_the_worker_flag_together_send_through_the_worker()
    {
        Assert.True(EmailDelivery.UseWorker(Config(
            ("ServiceBus:Namespace", "sb-test.servicebus.windows.net"),
            ("Notification:EmailWorker", "true"))));
    }

    [Fact]
    public async Task An_otp_email_with_service_bus_unset_is_queued_for_in_process_delivery()
    {
        var queue = new InProcessEmailQueue();
        var sender = new QueuedEmailSender(queue);

        await sender.SendAsync(new EmailMessage
        {
            ToEmail = "ravi@example.com",
            Subject = "Your code",
            HtmlBody = "<p>123456</p>",
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        EmailMessage queued = await queue.DequeueAsync(timeout.Token);

        Assert.Equal("ravi@example.com", queued.ToEmail);
        Assert.Equal("Your code", queued.Subject);
    }

    [Fact]
    public async Task The_worker_path_publishes_every_field_under_a_fresh_message_id()
    {
        var events = new RecordingPublisher();
        var sender = new EventEmailSender(events, TimeProvider.System);
        Guid customer = Guid.NewGuid();
        var message = new EmailMessage
        {
            CustomerId = customer,
            ToEmail = "ravi@example.com",
            ToName = "Ravi",
            Subject = "You're invited",
            HtmlBody = "<p>Join</p>",
            TextBody = "Join",
        };

        await sender.SendAsync(message);
        await sender.SendAsync(message);

        EmailRequested[] published = [.. events.Published.Cast<EmailRequested>()];
        Assert.Equal(2, published.Length);
        Assert.NotEqual(published[0].MessageId, published[1].MessageId);
        Assert.All(published, e => Assert.False(string.IsNullOrWhiteSpace(e.MessageId)));

        EmailMessage roundTrip = published[0].ToMessage();
        Assert.Equal(customer, roundTrip.CustomerId);
        Assert.Equal("ravi@example.com", roundTrip.ToEmail);
        Assert.Equal("Ravi", roundTrip.ToName);
        Assert.Equal("You're invited", roundTrip.Subject);
        Assert.Equal("<p>Join</p>", roundTrip.HtmlBody);
        Assert.Equal("Join", roundTrip.TextBody);
    }
}

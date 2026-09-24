using Azure.Messaging.ServiceBus;
using Notification.Worker.Consumers;
using Xunit;

namespace Notification.Worker.Tests;

/// <summary>Reading an <c>EmailRequested</c> off the wire (TK-19).</summary>
public sealed class EmailRequestedConsumerTests
{
    private static ServiceBusReceivedMessage Message(string body, string messageId = "transport-id") =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(body), messageId: messageId);

    [Fact]
    public void A_published_request_reads_back_with_its_own_message_id()
    {
        var request = Shared.Kernel.Messaging.EmailRequested.From(
            new Shared.Kernel.Interfaces.EmailMessage { ToEmail = "a@x.in", Subject = "S", HtmlBody = "<p>B</p>" },
            "body-id",
            DateTimeOffset.UnixEpoch);

        // The publisher's own wire shape, so the two cannot drift.
        ServiceBusMessage sent = Shared.Kernel.Messaging.ServiceBusEventPublisher.BuildMessage(request);

        var read = EmailRequestedConsumer.Read(Message(sent.Body.ToString()));

        Assert.NotNull(read);
        Assert.Equal("body-id", read!.MessageId);
        Assert.Equal("a@x.in", read.ToEmail);
        Assert.Equal("S", read.Subject);
    }

    [Fact]
    public void A_body_with_no_message_id_takes_the_transports()
    {
        var read = EmailRequestedConsumer.Read(Message(
            """{"messageId":"","toEmail":"a@x.in","subject":"S","htmlBody":"B","requestedAt":"2026-09-24T00:00:00Z"}"""));

        Assert.Equal("transport-id", read!.MessageId);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"subject":"no recipient","htmlBody":"B"}""")]
    public void An_unreadable_body_is_null_so_it_is_dead_lettered(string body)
    {
        Assert.Null(EmailRequestedConsumer.Read(Message(body)));
    }
}

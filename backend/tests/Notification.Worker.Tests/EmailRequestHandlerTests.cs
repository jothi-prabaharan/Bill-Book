using Microsoft.Extensions.Logging.Abstractions;
using Notification.Worker.Email;
using Notification.Worker.Persistence;
using Shared.Kernel.Email;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Messaging;
using Xunit;

namespace Notification.Worker.Tests;

/// <summary>
/// One email request, handled end to end with the store, Master and SMTP faked
/// (TK-19). The store's own guarantees are <see cref="ProcessedMessageStoreTests"/>'.
/// </summary>
public sealed class EmailRequestHandlerTests
{
    /// <summary>The store's rules, in memory: claim once, sent is final, release frees.</summary>
    private sealed class MemoryStore : IProcessedMessageStore
    {
        public Dictionary<string, bool> Rows { get; } = [];

        public Task<ClaimOutcome> TryClaimAsync(string messageId, string topic, CancellationToken ct)
        {
            if (Rows.TryGetValue(messageId, out bool sent))
            {
                return Task.FromResult(sent ? ClaimOutcome.AlreadySent : ClaimOutcome.InProgress);
            }

            Rows[messageId] = false;
            return Task.FromResult(ClaimOutcome.Claimed);
        }

        public Task MarkSentAsync(string messageId, CancellationToken ct)
        {
            Rows[messageId] = true;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string messageId, CancellationToken ct)
        {
            if (Rows.TryGetValue(messageId, out bool sent) && !sent)
            {
                Rows.Remove(messageId);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeDirectory : ISmtpDirectory
    {
        public ResolvedSmtp? Mailbox { get; set; } = new()
        {
            Host = "smtp.test",
            Port = 587,
            UseSsl = true,
            FromEmail = "no-reply@test",
            FromName = "Bill Book",
            Username = "u",
            Password = "p",
        };

        public List<Guid?> Asked { get; } = [];

        public Task<ResolvedSmtp?> ResolveAsync(Guid? customerId, CancellationToken ct)
        {
            Asked.Add(customerId);
            return Task.FromResult(Mailbox);
        }
    }

    private sealed class FakeTransport : IMailTransport
    {
        public List<EmailMessage> Sent { get; } = [];

        public int FailNext { get; set; }

        public Task SendAsync(ResolvedSmtp smtp, EmailMessage message, CancellationToken ct)
        {
            if (FailNext > 0)
            {
                FailNext--;
                throw new InvalidOperationException("SMTP refused");
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static EmailRequested Request(string id = "m-1", Guid? customerId = null) => new()
    {
        MessageId = id,
        RequestedAt = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero),
        CustomerId = customerId,
        ToEmail = "ravi@example.com",
        ToName = "Ravi",
        Subject = "You're invited",
        HtmlBody = "<p>Join</p>",
        TextBody = "Join",
    };

    private static (EmailRequestHandler Handler, MemoryStore Store, FakeDirectory Smtp, FakeTransport Transport) Setup()
    {
        var store = new MemoryStore();
        var smtp = new FakeDirectory();
        var transport = new FakeTransport();

        return (new EmailRequestHandler(store, smtp, transport, NullLogger<EmailRequestHandler>.Instance), store, smtp, transport);
    }

    [Fact]
    public async Task A_redelivered_message_sends_once()
    {
        (EmailRequestHandler handler, _, _, FakeTransport transport) = Setup();

        Assert.Equal(EmailHandleOutcome.Sent, await handler.HandleAsync(Request(), default));
        Assert.Equal(EmailHandleOutcome.Duplicate, await handler.HandleAsync(Request(), default));
        Assert.Equal(EmailHandleOutcome.Duplicate, await handler.HandleAsync(Request(), default));

        EmailMessage sent = Assert.Single(transport.Sent);
        Assert.Equal("ravi@example.com", sent.ToEmail);
        Assert.Equal("You're invited", sent.Subject);
        Assert.Equal("Join", sent.TextBody);
    }

    [Fact]
    public async Task A_failed_send_gives_the_claim_back_so_the_next_delivery_sends_it()
    {
        (EmailRequestHandler handler, MemoryStore store, _, FakeTransport transport) = Setup();
        transport.FailNext = 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(Request(), default));
        Assert.Empty(store.Rows);

        Assert.Equal(EmailHandleOutcome.Sent, await handler.HandleAsync(Request(), default));
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task A_message_another_delivery_holds_is_left_alone()
    {
        (EmailRequestHandler handler, MemoryStore store, _, FakeTransport transport) = Setup();
        store.Rows["m-1"] = false;

        Assert.Equal(EmailHandleOutcome.InProgress, await handler.HandleAsync(Request(), default));
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task No_mailbox_anywhere_sends_nothing_and_leaves_no_claim()
    {
        (EmailRequestHandler handler, MemoryStore store, FakeDirectory smtp, FakeTransport transport) = Setup();
        smtp.Mailbox = null;

        Assert.Equal(EmailHandleOutcome.NoMailbox, await handler.HandleAsync(Request(), default));
        Assert.Empty(transport.Sent);
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task The_mailbox_is_the_senders_customers()
    {
        (EmailRequestHandler handler, _, FakeDirectory smtp, _) = Setup();
        Guid customer = Guid.NewGuid();

        await handler.HandleAsync(Request(customerId: customer), default);
        await handler.HandleAsync(Request("m-2"), default);

        Assert.Equal([customer, null], smtp.Asked);
    }
}

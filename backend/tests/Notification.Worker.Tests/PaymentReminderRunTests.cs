using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Worker.Email;
using Notification.Worker.Persistence;
using Notification.Worker.Reminders;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Email;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Notification.Worker.Tests;

/// <summary>
/// One branch's payment reminders for one day (TK-20): an overdue, unpaid
/// invoice gets exactly one email per reminder window, and nothing crosses
/// between branches.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PaymentReminderRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;

    public PaymentReminderRunTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeSettlements : IInvoiceSettlements
    {
        public Dictionary<long, decimal> Outstanding { get; } = [];

        public bool Unreachable { get; set; }

        public Task<IReadOnlyDictionary<long, decimal>?> OutstandingAsync(
            Guid customerId, Guid orgId, IReadOnlyCollection<long> invoiceIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<long, decimal>?>(Unreachable ? null : Outstanding);
    }

    private sealed class FakeEmails : IContactEmails
    {
        public Dictionary<long, ContactEmail> Addresses { get; } = [];

        public Task<IReadOnlyDictionary<long, ContactEmail>?> ForAsync(
            Guid customerId, Guid orgId, IReadOnlyCollection<long> contactIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<long, ContactEmail>?>(Addresses);
    }

    private sealed class Mailbox : ISmtpDirectory
    {
        public Task<ResolvedSmtp?> ResolveAsync(Guid? customerId, CancellationToken ct) =>
            Task.FromResult<ResolvedSmtp?>(new ResolvedSmtp
            {
                Host = "smtp.test", Port = 587, UseSsl = true,
                FromEmail = "accounts@shop.in", FromName = "Shop", Username = "u", Password = "p",
            });
    }

    private sealed class Outbox : IMailTransport
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(ResolvedSmtp smtp, EmailMessage message, CancellationToken ct)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class Branch
    {
        public required TenantContext Tenant { get; init; }
        public FakeSettlements Settlements { get; } = new();
        public FakeEmails Emails { get; } = new();
    }

    private static Branch NewBranch(Guid? customerId = null) => new()
    {
        Tenant = new TenantContext { CustomerId = customerId ?? Guid.NewGuid(), OrgId = Guid.NewGuid() },
    };

    /// <summary>A run as the worker builds one: its own contexts, the real store and handler.</summary>
    private async Task<ReminderRunResult> RunAsync(Branch branch, Outbox outbox, Clock clock)
    {
        await using SalesDbContext sales = _postgres.CreateSalesContext(branch.Tenant);
        await using NotificationDbContext ntf = _postgres.CreateContext();

        var handler = new EmailRequestHandler(
            new ProcessedMessageStore(ntf, clock), new Mailbox(), outbox, NullLogger<EmailRequestHandler>.Instance);

        var run = new PaymentReminderRun(
            sales, branch.Tenant, branch.Settlements, branch.Emails, handler, clock,
            NullLogger<PaymentReminderRun>.Instance);

        return await run.RunAsync(default);
    }

    private async Task<int> SeedProfileAsync(Branch branch, int daysOverdue = 7)
    {
        await using SalesDbContext db = _postgres.CreateSalesContext(branch.Tenant);
        var profile = new ReminderProfile { ProfileName = "A week late", DaysOverdueTrigger = daysOverdue, IsActive = true };
        db.ReminderProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile.ReminderProfileId;
    }

    private async Task<long> SeedInvoiceAsync(
        Branch branch, DateOnly dueDate, long contactId = 42, DocumentStatus status = DocumentStatus.Posted, decimal total = 1180m)
    {
        await using SalesDbContext db = _postgres.CreateSalesContext(branch.Tenant);
        var invoice = new Invoice
        {
            TransactionTypeCode = "INV",
            DocumentNo = $"INV/{Guid.NewGuid():N}"[..20],
            DocumentDate = dueDate.AddDays(-30),
            DueDate = dueDate,
            ContactId = contactId,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            TotalAmount = total,
            Status = status,
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        branch.Settlements.Outstanding[invoice.InvoiceId] = total;
        branch.Emails.Addresses[contactId] = new ContactEmail(contactId, "ravi@example.com", "Ravi Stores");
        return invoice.InvoiceId;
    }

    private async Task<int> LogCountAsync(Branch branch, long invoiceId)
    {
        await using SalesDbContext db = _postgres.CreateSalesContext(branch.Tenant);
        return await db.ReminderLogs.CountAsync(l => l.InvoiceId == invoiceId);
    }

    [SkippableFact]
    public async Task An_overdue_unpaid_invoice_gets_one_email_and_one_log_row()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = NewBranch();
        await SeedProfileAsync(branch);
        long invoiceId = await SeedInvoiceAsync(branch, new DateOnly(2026, 9, 1));
        var outbox = new Outbox();

        ReminderRunResult result = await RunAsync(branch, outbox, new Clock(Now));

        Assert.Equal(1, result.Sent);
        EmailMessage mail = Assert.Single(outbox.Sent);
        Assert.Equal("ravi@example.com", mail.ToEmail);
        Assert.Equal(branch.Tenant.CustomerId, mail.CustomerId);
        Assert.Contains("1,180.00", mail.TextBody);
        Assert.Equal(1, await LogCountAsync(branch, invoiceId));
    }

    [SkippableFact]
    public async Task A_paid_invoice_gets_no_reminder()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = NewBranch();
        await SeedProfileAsync(branch);
        long invoiceId = await SeedInvoiceAsync(branch, new DateOnly(2026, 9, 1));
        branch.Settlements.Outstanding[invoiceId] = 0m;
        var outbox = new Outbox();

        ReminderRunResult result = await RunAsync(branch, outbox, new Clock(Now));

        Assert.Equal(1, result.AlreadyPaid);
        Assert.Empty(outbox.Sent);
        Assert.Equal(0, await LogCountAsync(branch, invoiceId));
    }

    [SkippableFact]
    public async Task A_second_run_on_the_same_day_sends_nothing()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = NewBranch();
        await SeedProfileAsync(branch);
        long invoiceId = await SeedInvoiceAsync(branch, new DateOnly(2026, 9, 1));
        var outbox = new Outbox();
        var clock = new Clock(Now);

        await RunAsync(branch, outbox, clock);
        clock.Now = Now.AddHours(6);
        ReminderRunResult second = await RunAsync(branch, outbox, clock);

        Assert.Equal(0, second.Sent);
        Assert.Single(outbox.Sent);
        Assert.Equal(1, await LogCountAsync(branch, invoiceId));
    }

    [SkippableFact]
    public async Task The_next_reminder_waits_for_the_window_to_pass()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = NewBranch();
        await SeedProfileAsync(branch);
        await SeedInvoiceAsync(branch, new DateOnly(2026, 9, 1));
        var outbox = new Outbox();
        var clock = new Clock(Now);

        await RunAsync(branch, outbox, clock);

        clock.Now = Now.AddDays(3);
        await RunAsync(branch, outbox, clock);
        Assert.Single(outbox.Sent);

        clock.Now = Now + PaymentReminderRun.ReminderInterval + TimeSpan.FromHours(1);
        await RunAsync(branch, outbox, clock);
        Assert.Equal(2, outbox.Sent.Count);
    }

    [SkippableFact]
    public async Task Branch_As_profile_never_reminds_branch_Bs_invoice()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Branch a = NewBranch(customerId);
        Branch b = NewBranch(customerId);

        // A has a profile and no invoices; B has an overdue invoice and no profile.
        await SeedProfileAsync(a);
        long bInvoice = await SeedInvoiceAsync(b, new DateOnly(2026, 9, 1));

        // Even told B's invoice is owed, A's run finds nothing to remind.
        a.Settlements.Outstanding[bInvoice] = 1180m;
        a.Emails.Addresses[42] = new ContactEmail(42, "ravi@example.com", "Ravi Stores");

        var outbox = new Outbox();
        Assert.Equal(0, (await RunAsync(a, outbox, new Clock(Now))).Sent);
        Assert.Equal(0, (await RunAsync(b, outbox, new Clock(Now))).Sent);

        Assert.Empty(outbox.Sent);
        Assert.Equal(0, await LogCountAsync(b, bInvoice));
    }

    [SkippableFact]
    public async Task Nothing_is_sent_when_accounting_cannot_say_what_is_owed()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = NewBranch();
        await SeedProfileAsync(branch);
        await SeedInvoiceAsync(branch, new DateOnly(2026, 9, 1));
        branch.Settlements.Unreachable = true;
        var outbox = new Outbox();

        ReminderRunResult result = await RunAsync(branch, outbox, new Clock(Now));

        Assert.True(result.Stopped);
        Assert.Empty(outbox.Sent);
    }

    [SkippableFact]
    public async Task A_customer_with_no_email_is_skipped_and_a_draft_or_not_yet_due_invoice_is_left_alone()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = NewBranch();
        await SeedProfileAsync(branch, daysOverdue: 7);

        long noEmail = await SeedInvoiceAsync(branch, new DateOnly(2026, 9, 1), contactId: 7);
        branch.Emails.Addresses.Remove(7);

        await SeedInvoiceAsync(branch, new DateOnly(2026, 9, 1), contactId: 8, status: DocumentStatus.Draft);

        // Due five days ago: not yet the profile's seven.
        await SeedInvoiceAsync(branch, new DateOnly(2026, 9, 19), contactId: 9);

        var outbox = new Outbox();
        ReminderRunResult result = await RunAsync(branch, outbox, new Clock(Now));

        Assert.Equal(0, result.Sent);
        Assert.Equal(1, result.NoEmail);
        Assert.Empty(outbox.Sent);
        Assert.Equal(0, await LogCountAsync(branch, noEmail));
    }

    [Fact]
    public void The_dedupe_key_is_fixed_by_branch_invoice_profile_and_day()
    {
        Guid org = Guid.NewGuid();

        Assert.Equal(
            PaymentReminderRun.MessageIdFor(org, 5, 1, new DateOnly(2026, 9, 24)),
            PaymentReminderRun.MessageIdFor(org, 5, 1, new DateOnly(2026, 9, 24)));
        Assert.NotEqual(
            PaymentReminderRun.MessageIdFor(org, 5, 1, new DateOnly(2026, 9, 24)),
            PaymentReminderRun.MessageIdFor(org, 5, 1, new DateOnly(2026, 9, 25)));
        Assert.True(PaymentReminderRun.MessageIdFor(org, long.MaxValue, int.MaxValue, DateOnly.MaxValue).Length <= 64);
    }
}

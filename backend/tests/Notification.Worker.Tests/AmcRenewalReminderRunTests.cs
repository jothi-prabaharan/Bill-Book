using Microsoft.Extensions.Logging.Abstractions;
using Notification.Worker.Email;
using Notification.Worker.Persistence;
using Notification.Worker.Reminders;
using Shared.Kernel.Email;
using Shared.Kernel.Interfaces;
using Shared.Kernel.School;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Notification.Worker.Tests;

/// <summary>
/// One branch's AMC renewal reminders (S8, TK-68): a contract in its window
/// gets one email before it expires, and one only, however many daily runs
/// fall in the window.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AmcRenewalReminderRunTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 6, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;

    public AmcRenewalReminderRunTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeRenewals : IAmcRenewals
    {
        public List<AmcRenewalDue> Due { get; } = [];

        public bool Unreachable { get; set; }

        public Task<IReadOnlyList<AmcRenewalDue>?> DueAsync(Guid customerId, Guid orgId, DateOnly on, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AmcRenewalDue>?>(Unreachable ? null : Due);
    }

    private sealed class Mailbox : ISmtpDirectory
    {
        public Task<ResolvedSmtp?> ResolveAsync(Guid? customerId, CancellationToken ct) =>
            Task.FromResult<ResolvedSmtp?>(new ResolvedSmtp
            {
                Host = "smtp.test", Port = 587, UseSsl = true,
                FromEmail = "office@school.in", FromName = "School", Username = "u", Password = "p",
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

    private static AmcRenewalDue CoolAir(long id = 1, string? email = "office@school.in") => new()
    {
        AmcContractId = id,
        ContractNo = "CA/2026/114",
        VendorName = "CoolAir Services",
        EndDate = new DateOnly(2027, 3, 31),
        ContractValue = 48000m,
        ReminderEmail = email,
    };

    private async Task<AmcRenewalRunResult> RunAsync(TenantContext tenant, FakeRenewals renewals, Outbox outbox, Clock clock)
    {
        await using NotificationDbContext ntf = _postgres.CreateContext();
        var handler = new EmailRequestHandler(
            new ProcessedMessageStore(ntf, clock), new Mailbox(), outbox, NullLogger<EmailRequestHandler>.Instance);
        return await new AmcRenewalReminderRun(tenant, renewals, handler, clock, NullLogger<AmcRenewalReminderRun>.Instance).RunAsync(default);
    }

    private static TenantContext NewBranch() => new() { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };

    [SkippableFact]
    public async Task A_contract_in_its_window_gets_one_reminder_before_it_expires()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        TenantContext branch = NewBranch();
        var renewals = new FakeRenewals();
        renewals.Due.Add(CoolAir());
        var outbox = new Outbox();

        AmcRenewalRunResult result = await RunAsync(branch, renewals, outbox, new Clock(Now));

        Assert.Equal(1, result.Sent);
        EmailMessage mail = Assert.Single(outbox.Sent);
        Assert.Equal("office@school.in", mail.ToEmail);
        Assert.Equal(branch.CustomerId, mail.CustomerId);
        Assert.Contains("CA/2026/114", mail.Subject);
        Assert.Contains("in 30 days", mail.TextBody);
        Assert.Contains("CoolAir Services", mail.TextBody);
    }

    [SkippableFact]
    public async Task The_next_days_run_sends_nothing_more_for_the_same_term()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        TenantContext branch = NewBranch();
        var renewals = new FakeRenewals();
        renewals.Due.Add(CoolAir());
        var outbox = new Outbox();
        var clock = new Clock(Now);

        await RunAsync(branch, renewals, outbox, clock);
        clock.Now = Now.AddDays(1);
        AmcRenewalRunResult second = await RunAsync(branch, renewals, outbox, clock);

        Assert.Equal(0, second.Sent);
        Assert.Single(outbox.Sent);
    }

    [SkippableFact]
    public async Task A_contract_with_no_address_is_counted_and_skipped()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var renewals = new FakeRenewals();
        renewals.Due.Add(CoolAir(email: null));
        var outbox = new Outbox();

        AmcRenewalRunResult result = await RunAsync(NewBranch(), renewals, outbox, new Clock(Now));

        Assert.Equal(1, result.NoEmail);
        Assert.Empty(outbox.Sent);
    }

    [SkippableFact]
    public async Task Amc_unreachable_sends_nothing_and_says_so()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var renewals = new FakeRenewals { Unreachable = true };
        var outbox = new Outbox();

        AmcRenewalRunResult result = await RunAsync(NewBranch(), renewals, outbox, new Clock(Now));

        Assert.True(result.Stopped);
        Assert.Empty(outbox.Sent);
    }

    [Fact]
    public void The_message_id_is_fixed_by_branch_contract_and_term()
    {
        Guid org = Guid.NewGuid();
        string id = AmcRenewalReminderRun.MessageIdFor(org, 1, new DateOnly(2027, 3, 31));

        Assert.Equal(id, AmcRenewalReminderRun.MessageIdFor(org, 1, new DateOnly(2027, 3, 31)));
        Assert.NotEqual(id, AmcRenewalReminderRun.MessageIdFor(org, 1, new DateOnly(2028, 3, 31)));
        Assert.NotEqual(id, AmcRenewalReminderRun.MessageIdFor(Guid.NewGuid(), 1, new DateOnly(2027, 3, 31)));
        Assert.True(id.Length <= 64);
    }
}

using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Approvals;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Approval chains on manual journals and spend money (TK-101), through the
/// services the controllers use and against a real PostgreSQL, with Master's
/// chain resolver stood in for: "an Accountant, then a second Accountant".
/// Both levels name the same role, which is exactly the case where one
/// person could otherwise approve twice.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AccountingApprovalTests
{
    private const int AccountantRole = 3;

    private static readonly Guid Clerk = Guid.NewGuid();
    private static readonly Guid FirstAccountant = Guid.NewGuid();
    private static readonly Guid SecondAccountant = Guid.NewGuid();

    private readonly PostgresFixture _postgres;

    public AccountingApprovalTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>The card's "done when": a journal configured for two levels posts only after both.</summary>
    [SkippableFact]
    public async Task A_journal_posts_only_after_both_levels_approve()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long journal = await h.JournalAsync(500m);

        Assert.Equal(SaveJournalOutcome.AwaitingApproval, (await h.Journals.PostAsync(journal, default)).Outcome);

        Assert.Equal(ApprovalResultOutcome.Ok, (await h.Approvals.SubmitAsync(ApprovalRequestKind.ManualJournal, journal, default)).Outcome);
        Assert.Equal(SaveJournalOutcome.AwaitingApproval, (await h.Journals.PostAsync(journal, default)).Outcome);

        h.User.Become(FirstAccountant, AccountantRole);
        Assert.Equal(ApprovalResultOutcome.Ok, (await h.Approvals.ActAsync(ApprovalRequestKind.ManualJournal, journal, ApprovalAction.Approve, null, default)).Outcome);
        Assert.Equal("Second accountant", (await h.ReadJournalAsync(journal)).CurrentStepLabel);
        Assert.Equal(SaveJournalOutcome.AwaitingApproval, (await h.Journals.PostAsync(journal, default)).Outcome);

        h.User.Become(SecondAccountant, AccountantRole);
        Assert.Equal(ApprovalResultOutcome.Ok, (await h.Approvals.ActAsync(ApprovalRequestKind.ManualJournal, journal, ApprovalAction.Approve, null, default)).Outcome);
        Assert.Equal(ApprovalStatus.Approved, (await h.ReadJournalAsync(journal)).ApprovalStatus);

        Assert.Equal(SaveJournalOutcome.Ok, (await h.Journals.PostAsync(journal, default)).Outcome);
        Assert.Equal(JournalStatus.Posted, (await h.ReadJournalAsync(journal)).Status);
    }

    [SkippableFact]
    public async Task The_second_approver_cannot_be_the_first()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long journal = await h.JournalAsync(500m);
        await h.Approvals.SubmitAsync(ApprovalRequestKind.ManualJournal, journal, default);

        h.User.Become(FirstAccountant, AccountantRole);
        await h.Approvals.ActAsync(ApprovalRequestKind.ManualJournal, journal, ApprovalAction.Approve, null, default);

        ApprovalResult again = await h.Approvals.ActAsync(
            ApprovalRequestKind.ManualJournal, journal, ApprovalAction.Approve, null, default);

        Assert.Equal(ApprovalResultOutcome.Refused, again.Outcome);
        Assert.Equal(ApprovalChain.Message(ApprovalRefusal.ApprovedEarlierLevel), again.Detail);

        Journal still = await h.ReadJournalAsync(journal);
        Assert.Equal(ApprovalStatus.InApproval, still.ApprovalStatus);
        Assert.Equal("Second accountant", still.CurrentStepLabel);
    }

    [SkippableFact]
    public async Task Editing_a_journal_mid_chain_returns_it_to_draft_and_a_new_round_starts_over()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long journal = await h.JournalAsync(500m);
        await h.Approvals.SubmitAsync(ApprovalRequestKind.ManualJournal, journal, default);
        h.User.Become(FirstAccountant, AccountantRole);
        await h.Approvals.ActAsync(ApprovalRequestKind.ManualJournal, journal, ApprovalAction.Approve, null, default);

        h.User.Become(Clerk, role: 5);
        SaveJournalResult edited = await h.Journals.UpdateAsync(journal, h.Entry(700m), default);

        Assert.Equal(SaveJournalOutcome.Ok, edited.Outcome);
        Assert.NotNull(edited.Detail);
        Assert.Null((await h.ReadJournalAsync(journal)).ApprovalStatus);

        List<AccountingApprovalStep> steps = await h.Db.ApprovalSteps.AsNoTracking().OrderBy(s => s.Sequence).ToListAsync();
        Assert.Equal([ApprovalStepStatus.Approved, ApprovalStepStatus.Cancelled], steps.Select(s => s.StepStatus));

        // The old approval does not count: the first accountant may take level 1 again.
        await h.Approvals.SubmitAsync(ApprovalRequestKind.ManualJournal, journal, default);
        h.User.Become(FirstAccountant, AccountantRole);
        Assert.Equal(ApprovalResultOutcome.Ok, (await h.Approvals.ActAsync(ApprovalRequestKind.ManualJournal, journal, ApprovalAction.Approve, null, default)).Outcome);
        Assert.Equal(2, await h.Db.ApprovalSteps.AsNoTracking().CountAsync(s => s.Round == 2));
    }

    [SkippableFact]
    public async Task The_resolver_is_asked_with_the_journals_debit_total()
    {
        var chains = new FakeChains();
        await using Harness h = await Harness.CreateAsync(_postgres, chains);
        long journal = await h.JournalAsync(1_234.50m);

        await h.Approvals.SubmitAsync(ApprovalRequestKind.ManualJournal, journal, default);

        Assert.Equal(1_234.50m, chains.LastAmount);
    }

    [SkippableFact]
    public async Task With_no_workflow_a_journal_posts_as_it_always_has()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, new FakeChains { NoWorkflow = true });
        long journal = await h.JournalAsync(500m);

        Assert.Equal(SaveJournalOutcome.Ok, (await h.Journals.PostAsync(journal, default)).Outcome);
    }

    [SkippableFact]
    public async Task An_unreachable_master_refuses_the_post_rather_than_reading_it_as_no_rules()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, new FakeChains { Unreachable = true });
        long journal = await h.JournalAsync(500m);

        Assert.Equal(SaveJournalOutcome.AwaitingApproval, (await h.Journals.PostAsync(journal, default)).Outcome);
        Assert.Equal(JournalStatus.Draft, (await h.ReadJournalAsync(journal)).Status);
    }

    [SkippableFact]
    public async Task A_system_journal_is_not_gated()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, new FakeChains { Unreachable = true });

        SaveJournalResult posted = await h.Journals.PostSystemAsync(h.Entry(500m), ledgerSourceId: 12, default);

        Assert.Equal(SaveJournalOutcome.Ok, posted.Outcome);
    }

    [SkippableFact]
    public async Task A_spend_money_draft_is_gated_and_named_in_the_inbox()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long payment = await h.PaymentAsync(2_000m);

        Assert.Equal(MoneyDocumentOutcome.AwaitingApproval, (await h.Payments.PostAsync(payment, default)).Outcome);

        await h.Approvals.SubmitAsync(ApprovalRequestKind.SpendMoney, payment, default);

        h.User.Become(FirstAccountant, AccountantRole);
        ApprovalInboxItem waiting = Assert.Single(await h.Approvals.MineAsync(default));
        Assert.Equal(("spend-money", payment, 2_000m), (waiting.Document, waiting.RequestId, waiting.Amount));
        Assert.StartsWith("Draft payment", waiting.DocumentNo);
    }

    /// <summary>A user whose identity a test can change between calls.</summary>
    private sealed class SwitchableUser : ICurrentUser
    {
        public Guid? UserId { get; private set; } = Clerk;

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId { get; private set; } = 5;

        public void Become(Guid userId, int role)
        {
            UserId = userId;
            RoleId = role;
        }
    }

    /// <summary>Master's resolver: two levels, both the Accountant role.</summary>
    private sealed class FakeChains : IApprovalChainClient
    {
        public bool NoWorkflow { get; init; }

        public bool Unreachable { get; init; }

        public decimal? LastAmount { get; private set; }

        public Task<ResolveChainResponse?> ResolveAsync(ResolveChainRequest request, CancellationToken ct)
        {
            LastAmount = request.Amount;

            if (Unreachable)
            {
                return Task.FromResult<ResolveChainResponse?>(null);
            }

            if (NoWorkflow)
            {
                return Task.FromResult<ResolveChainResponse?>(new ResolveChainResponse { Outcome = ResolveChainOutcome.NoWorkflow });
            }

            return Task.FromResult<ResolveChainResponse?>(new ResolveChainResponse
            {
                Outcome = ResolveChainOutcome.Resolved,
                WorkflowName = "Manual journals",
                Steps =
                [
                    new ResolvedStep { Sequence = 1, Label = "Accountant", RoleId = AccountantRole },
                    new ResolvedStep { Sequence = 2, Label = "Second accountant", RoleId = AccountantRole },
                ],
            });
        }

        public Task<bool> IsDelegateAsync(DelegateCheckRequest request, CancellationToken ct) => Task.FromResult(false);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required JournalService Journals { get; init; }

        public required SpendMoneyService Payments { get; init; }

        public required AccountingApprovalService Approvals { get; init; }

        public required SwitchableUser User { get; init; }

        public required long RentId { get; init; }

        public required long CashId { get; init; }

        public required long BankAccountId { get; init; }

        public SaveJournalRequest Entry(decimal amount) => new()
        {
            JournalDate = new DateOnly(2026, 8, 1),
            Reference = "Accrual",
            Lines =
            [
                new SaveJournalLineRequest { AccountId = RentId, DebitAmount = amount },
                new SaveJournalLineRequest { AccountId = CashId, CreditAmount = amount },
            ],
        };

        public async Task<long> JournalAsync(decimal amount)
        {
            SaveJournalResult created = await Journals.CreateAsync(Entry(amount), default);
            Assert.Equal(SaveJournalOutcome.Ok, created.Outcome);
            Db.ChangeTracker.Clear();
            return created.JournalId;
        }

        public async Task<long> PaymentAsync(decimal amount)
        {
            // A draft needs no lines, and the gate is asked before any are read.
            var payment = new SpendMoney
            {
                TransactionDate = new DateOnly(2026, 8, 1),
                BankAccountId = BankAccountId,
                ContactId = 42,
                Amount = amount,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                Status = MoneyDocumentStatus.Draft,
            };

            Db.SpendMoney.Add(payment);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return payment.SpendMoneyId;
        }

        public async Task<Journal> ReadJournalAsync(long id)
        {
            Db.ChangeTracker.Clear();
            return await Db.Journals.AsNoTracking().SingleAsync(j => j.JournalId == id);
        }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres, FakeChains? chains = null)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(tenant.CustomerId!.Value, orgId);

            async Task<long> Account(string code, string name, int typeId)
            {
                var account = new Account
                {
                    OrgId = orgId,
                    AccountTypeId = typeId,
                    AccountCode = code,
                    AccountName = name,
                    IsActive = true,
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            long rent = await Account("6100", "Rent", 5);
            long cash = await Account("1010", "Cash", 1);
            long bankLedger = await Account("1020", "Bank", 1);

            var bankAccount = new BankAccount
            {
                OrgId = orgId,
                AccountName = "Current",
                AccountNumber = "000111",
                CurrencyCode = "INR",
                LedgerAccountId = bankLedger,
                IsActive = true,
            };
            db.BankAccounts.Add(bankAccount);
            await db.SaveChangesAsync();

            var user = new SwitchableUser();
            var numbers = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
            var approvals = new AccountingApprovalService(db, chains ?? new FakeChains(), tenant, user, TimeProvider.System);

            return new Harness
            {
                Db = db,
                User = user,
                Approvals = approvals,
                RentId = rent,
                CashId = cash,
                BankAccountId = bankAccount.BankAccountId,
                Journals = new JournalService(
                    db,
                    new LedgerPostingService(db, tenant, new StubBaseCurrency()),
                    new PeriodLockService(db, new StubCurrentUser()),
                    numbers,
                    new StubBaseCurrency(),
                    user,
                    tenant,
                    TimeProvider.System,
                    approvals),
                Payments = new SpendMoneyService(
                    db,
                    new RecordingLedger(),
                    numbers,
                    new StubBaseCurrency(),
                    user,
                    TimeProvider.System,
                    approvals),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

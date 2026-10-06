using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Jewellery savings schemes (TK-141). The card's Done-when line: a customer
/// enrols, pays instalments and redeems against an invoice. The rest pins the
/// decisions settled in the card: the bonus goes on at redemption as an
/// expense, a lapse forfeits it, and a refund returns what was paid and no more.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SavingsSchemeTests
{
    private const long Customer = 7;
    private const long Invoice = 5001;

    private readonly PostgresFixture _postgres;

    public SavingsSchemeTests(PostgresFixture postgres) => _postgres = postgres;

    // ---- The rules, without a database ------------------------------------

    [Theory]
    [InlineData(SchemeBonusType.None, 0, 0)]
    [InlineData(SchemeBonusType.FixedAmount, 750, 750)]
    [InlineData(SchemeBonusType.Instalments, 1, 1000)]
    [InlineData(SchemeBonusType.Percent, 5, 550)]
    public void The_bonus_follows_the_scheme_rule(SchemeBonusType type, decimal value, decimal expected) =>
        Assert.Equal(expected, SavingsSchemeRulesRepository.Bonus(type, value, instalment: 1000m, months: 11));

    [Fact]
    public void Instalments_fall_monthly_from_the_start_date()
    {
        var schedule = SavingsSchemeRulesRepository.Schedule(new DateOnly(2026, 1, 31), 3).ToList();

        Assert.Equal([new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31)], schedule.Select(s => s.Due));
        Assert.Equal([1, 2, 3], schedule.Select(s => s.Number));
    }

    [Fact]
    public void Bonus_is_applied_first_and_nothing_goes_past_what_the_invoice_owes()
    {
        Assert.Equal((1000m, 2000m), SavingsSchemeRulesRepository.Split(bonus: 1000m, principal: 2000m, outstanding: 5000m));
        Assert.Equal((1000m, 500m), SavingsSchemeRulesRepository.Split(bonus: 1000m, principal: 2000m, outstanding: 1500m));
        Assert.Equal((800m, 0m), SavingsSchemeRulesRepository.Split(bonus: 1000m, principal: 2000m, outstanding: 800m));
    }

    [Fact]
    public void An_instalment_lapses_only_after_its_grace_days()
    {
        DateOnly due = new(2026, 3, 1);

        Assert.False(SavingsSchemeRulesRepository.IsLapsed(due, graceDays: 30, today: new DateOnly(2026, 3, 31)));
        Assert.True(SavingsSchemeRulesRepository.IsLapsed(due, graceDays: 30, today: new DateOnly(2026, 4, 1)));
    }

    // ---- Against a database -----------------------------------------------

    [SkippableFact]
    public async Task A_customer_enrols_pays_every_instalment_and_redeems_it_with_the_bonus_against_an_invoice()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, invoiceTotal: 5000m);

        long enrolmentId = await h.EnrolAsync(months: 2, instalment: 1000m, bonusInstalments: 1m);

        Assert.Equal(SavingsSchemeOutcome.Ok, (await h.PayAsync(enrolmentId)).Outcome);
        Assert.Equal(SavingsSchemeOutcome.Ok, (await h.PayAsync(enrolmentId)).Outcome);

        SchemeEnrolmentView? matured = await h.Schemes.GetEnrolmentAsync(enrolmentId, default);
        Assert.Equal("Matured", matured!.Status);
        Assert.Equal(2000m, matured.PaidPrincipal);
        Assert.Equal(2000m, matured.PrincipalAvailable);

        // Each instalment was a customer prepayment (source 9) receipt.
        Assert.Equal(2, h.Ledger.Postings.Count);

        SavingsSchemeResult redeemed = await h.Schemes.RedeemAsync(enrolmentId, new RedeemEnrolmentRequest
        {
            InvoiceId = Invoice,
            RedemptionDate = new DateOnly(2026, 4, 1),
        }, default);
        Assert.Equal(SavingsSchemeOutcome.Ok, redeemed.Outcome);

        await using AccountingDbContext check = h.Check();
        SchemeEnrolment after = await check.SchemeEnrolments.AsNoTracking().SingleAsync();
        Assert.Equal(EnrolmentStatus.Redeemed, after.Status);
        Assert.Equal(2000m, after.PrincipalRedeemed);
        Assert.Equal(1000m, after.BonusRedeemed);

        List<TransactionRatio> claims = await check.TransactionRatios.AsNoTracking()
            .Where(t => t.TargetTransactionTypeCode == "INV" && t.TargetTransactionId == Invoice && !t.IsVoided)
            .ToListAsync();
        Assert.Equal(2000m, claims.Where(t => t.SourceTransactionTypeCode == "RCM").Sum(t => t.Amount));
        Assert.Equal(1000m, claims.Single(t => t.SourceTransactionTypeCode == "JRN").Amount);

        // The bonus is the jeweller's expense, crediting the customer's receivable.
        List<JournalLedger> bonus = await check.JournalLedger.AsNoTracking()
            .Where(l => l.TransactionTypeCode == "JRN" && l.TransactionId == after.BonusJournalId)
            .ToListAsync();
        Assert.Equal(1000m, bonus.Single(l => l.AccountId == h.BonusAccountId).DebitAmount);
        Assert.Equal(1000m, bonus.Single(l => l.AccountId == h.ReceivableAccountId).CreditAmount);
    }

    [SkippableFact]
    public async Task An_unmatured_enrolment_redeems_only_when_closed_early_and_then_without_its_bonus()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, invoiceTotal: 5000m);
        long enrolmentId = await h.EnrolAsync(months: 3, instalment: 1000m, bonusInstalments: 1m);
        await h.PayAsync(enrolmentId);

        SavingsSchemeResult refused = await h.Schemes.RedeemAsync(enrolmentId, new RedeemEnrolmentRequest
        {
            InvoiceId = Invoice,
            RedemptionDate = new DateOnly(2026, 2, 1),
        }, default);
        Assert.Equal(SavingsSchemeOutcome.Conflict, refused.Outcome);

        SavingsSchemeResult early = await h.Schemes.RedeemAsync(enrolmentId, new RedeemEnrolmentRequest
        {
            InvoiceId = Invoice,
            RedemptionDate = new DateOnly(2026, 2, 1),
            CloseEarly = true,
        }, default);
        Assert.Equal(SavingsSchemeOutcome.Ok, early.Outcome);

        await using AccountingDbContext check = h.Check();
        SchemeEnrolment after = await check.SchemeEnrolments.AsNoTracking().SingleAsync();
        Assert.Equal(1000m, after.PrincipalRedeemed);
        Assert.Equal(0m, after.BonusRedeemed);
        Assert.Null(after.BonusJournalId);
    }

    [SkippableFact]
    public async Task An_instalment_left_unpaid_past_its_grace_days_lapses_and_reminders_name_the_rest()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, invoiceTotal: 5000m);
        long late = await h.EnrolAsync(months: 3, instalment: 1000m, bonusInstalments: 1m, start: new DateOnly(2026, 1, 1));
        long dueSoon = await h.EnrolAsync(months: 3, instalment: 1000m, bonusInstalments: 1m, start: new DateOnly(2026, 3, 3), code: "GOLD2");

        RunSavingsSchemesResponse run = await h.Schemes.RunAsync(new DateOnly(2026, 3, 1), default);

        Assert.Equal(1, run.Lapsed);
        SchemeReminder reminder = Assert.Single(run.Reminders);
        Assert.Equal("Due", reminder.Kind);
        Assert.Equal(new DateOnly(2026, 3, 3), reminder.DueDate);

        await using AccountingDbContext check = h.Check();
        Assert.Equal(EnrolmentStatus.Lapsed, (await check.SchemeEnrolments.AsNoTracking().SingleAsync(e => e.SchemeEnrolmentId == late)).Status);
        Assert.Equal(EnrolmentStatus.Active, (await check.SchemeEnrolments.AsNoTracking().SingleAsync(e => e.SchemeEnrolmentId == dueSoon)).Status);

        Assert.Equal(SavingsSchemeOutcome.Conflict, (await h.PayAsync(late)).Outcome);
    }

    [SkippableFact]
    public async Task A_refund_returns_what_was_paid_and_never_the_bonus()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, invoiceTotal: 5000m);
        long enrolmentId = await h.EnrolAsync(months: 2, instalment: 1000m, bonusInstalments: 1m);
        await h.PayAsync(enrolmentId);
        await h.PayAsync(enrolmentId);

        SavingsSchemeResult refunded = await h.Schemes.RefundAsync(enrolmentId, new RefundEnrolmentRequest
        {
            BankAccountId = h.BankAccountId,
            RefundDate = new DateOnly(2026, 4, 1),
        }, default);
        Assert.True(refunded.Outcome == SavingsSchemeOutcome.Ok, refunded.Detail ?? "No detail");

        await using AccountingDbContext check = h.Check();
        SpendMoney refund = await check.SpendMoney.AsNoTracking().SingleAsync(s => s.SpendMoneyId == refunded.Id);
        Assert.Equal(2000m, refund.Amount);
        Assert.Equal(EnrolmentStatus.Refunded, (await check.SchemeEnrolments.AsNoTracking().SingleAsync()).Status);

        SchemeEnrolmentView? view = await h.Schemes.GetEnrolmentAsync(enrolmentId, default);
        Assert.Equal(0m, view!.PrincipalAvailable);
    }

    [SkippableFact]
    public async Task A_customer_with_no_advance_account_cannot_enrol()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, invoiceTotal: 5000m);
        long schemeId = await h.SchemeAsync("GOLD", months: 11, instalment: 1000m, bonusInstalments: 1m);

        SavingsSchemeResult result = await h.Schemes.EnrolAsync(new EnrolRequest
        {
            SavingsSchemeId = schemeId,
            ContactId = 999,
            StartDate = new DateOnly(2026, 1, 1),
        }, default);

        Assert.Equal(SavingsSchemeOutcome.Invalid, result.Outcome);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private PostgresFixture Postgres { get; init; } = null!;

        public required AccountingDbContext Db { get; init; }
        public required TenantContext Tenant { get; init; }
        public required SavingsSchemeRepository Schemes { get; init; }
        public required RecordingLedger Ledger { get; init; }
        public required long BankAccountId { get; init; }
        public required long BonusAccountId { get; init; }
        public required long ReceivableAccountId { get; init; }

        public AccountingDbContext Check() => Postgres.CreateContext(Tenant.CustomerId!.Value, Tenant.OrgId!.Value);

        public async Task<long> SchemeAsync(string code, int months, decimal instalment, decimal bonusInstalments)
        {
            SavingsSchemeResult created = await Schemes.SaveSchemeAsync(null, new SaveSavingsSchemeRequest
            {
                Code = code,
                Name = $"{code} scheme",
                InstalmentAmount = instalment,
                Months = months,
                BonusType = SchemeBonusType.Instalments,
                BonusValue = bonusInstalments,
                GraceDays = 30,
            }, default);

            Assert.Equal(SavingsSchemeOutcome.Ok, created.Outcome);
            return created.Id;
        }

        public async Task<long> EnrolAsync(
            int months, decimal instalment, decimal bonusInstalments, DateOnly? start = null, string code = "GOLD")
        {
            long schemeId = await SchemeAsync(code, months, instalment, bonusInstalments);
            SavingsSchemeResult enrolled = await Schemes.EnrolAsync(new EnrolRequest
            {
                SavingsSchemeId = schemeId,
                ContactId = Customer,
                StartDate = start ?? new DateOnly(2026, 1, 1),
            }, default);

            Assert.Equal(SavingsSchemeOutcome.Ok, enrolled.Outcome);
            return enrolled.Id;
        }

        public Task<SavingsSchemeResult> PayAsync(long enrolmentId) =>
            Schemes.PayInstalmentAsync(enrolmentId, new PayInstalmentRequest
            {
                BankAccountId = BankAccountId,
                PaymentDate = new DateOnly(2026, 1, 15),
                PaymentMethod = PaymentMethod.Cash,
            }, default);

        public static async Task<Harness> CreateAsync(PostgresFixture postgres, decimal invoiceTotal)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
            Guid orgId = tenant.OrgId!.Value;
            AccountingDbContext db = postgres.CreateContext(tenant.CustomerId!.Value, orgId);

            async Task<long> Account(string code, string name, int type)
            {
                var account = new Account
                {
                    OrgId = orgId, AccountCode = code, AccountName = name, AccountSystemName = name, AccountTypeId = type, IsActive = true,
                };
                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            async Task SubAccount(long accountId, int type, SubAccountPurpose purpose)
            {
                db.SubAccounts.Add(new SubAccount
                {
                    OrgId = orgId,
                    AccountId = accountId,
                    AccountTypeId = type,
                    ReferenceType = SubAccountReferenceType.Contact,
                    ReferenceId = Customer,
                    Purpose = purpose,
                    SubAccountName = $"Customer {purpose}",
                    IsActive = true,
                });
                await db.SaveChangesAsync();
            }

            long receivable = await Account("1100", "Accounts Receivable", 1);
            long payable = await Account("2100", "Accounts Payable", 2);
            long revenue = await Account("4100", "Sales Revenue", 4);
            long bonusAccount = await Account("5400", "Savings Scheme Bonus", 5);
            long cash = await Account("1410", "Counter cash", 1);

            await SubAccount(receivable, 1, SubAccountPurpose.Primary);
            await SubAccount(payable, 2, SubAccountPurpose.PrepaymentAdvance);

            // The invoice as Sales posted it: owed by the customer on the control leg.
            db.JournalLedger.AddRange(
                Leg(orgId, receivable, 3, debit: invoiceTotal),
                Leg(orgId, revenue, 1, credit: invoiceTotal));

            var bank = new BankAccount
            {
                OrgId = orgId,
                AccountName = "Counter cash",
                AccountNumber = "CASH-1",
                AccountType = BankAccountType.Cash,
                CurrencyCode = "INR",
                LedgerAccountId = cash,
                IsActive = true,
            };
            db.BankAccounts.Add(bank);
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var numbers = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
            var ledger = new RecordingLedger(db: db);
            var user = new StubCurrentUser();

            var journals = new JournalRepository(
                db, new LedgerPostingRepository(db, tenant, new StubBaseCurrency()),
                new PeriodLockRepository(db, user), numbers, new StubBaseCurrency(), user, tenant, TimeProvider.System);

            var schemes = new SavingsSchemeRepository(
                db,
                tenant,
                new ReceiveMoneyRepository(db, ledger, numbers, new StubBaseCurrency(), user, TimeProvider.System),
                new SpendMoneyRepository(db, ledger, numbers, new StubBaseCurrency(), user, TimeProvider.System),
                journals,
                new AllocationRepository(db, tenant, NullLogger<AllocationRepository>.Instance),
                TimeProvider.System);

            return new Harness
            {
                Postgres = postgres,
                Db = db,
                Tenant = tenant,
                Schemes = schemes,
                Ledger = ledger,
                BankAccountId = bank.BankAccountId,
                BonusAccountId = bonusAccount,
                ReceivableAccountId = receivable,
            };
        }

        private static JournalLedger Leg(Guid orgId, long accountId, int ledgerType, decimal debit = 0m, decimal credit = 0m) => new()
        {
            OrgId = orgId,
            LedgerDate = new DateOnly(2026, 4, 1),
            AccountId = accountId,
            TransactionTypeCode = "INV",
            TransactionId = Invoice,
            TransactionDetailId = 0,
            DebitAmount = debit,
            CreditAmount = credit,
            DebitAmountBase = debit,
            CreditAmountBase = credit,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            ContactId = Customer,
            LedgerTypeId = ledgerType,
            LedgerSourceId = 1,
            DocumentNo = "INV/26/5001",
        };

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

using Accounting.Api.Services;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The account ledger and the trial balance, read back out of a ledger that was
/// written through the posting door — not out of rows inserted by the test. The
/// point of these two screens is that a posting can be checked, and a test that
/// hand-wrote the rows would be checking its own arithmetic.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class LedgerReportServiceTests
{
    private const int Control = 3;

    private readonly PostgresFixture _postgres;

    public LedgerReportServiceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// A trial balance over a ledger that only ever took balanced postings has
    /// to foot. This is the one number that says the whole system is sound.
    /// </summary>
    [SkippableFact]
    public async Task A_trial_balance_over_real_postings_balances()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Post(1001, new DateOnly(2026, 7, 1), 500m, ct);
        await h.Post(1002, new DateOnly(2026, 7, 15), 250m, ct);
        await h.Post(1003, new DateOnly(2026, 8, 1), 125.55m, ct);

        TrialBalanceView trial = await h.Reports.GetTrialBalanceAsync(null, null, ct);

        Assert.True(trial.IsBalanced);
        Assert.Equal(875.55m, trial.TotalDebit);
        Assert.Equal(875.55m, trial.TotalCredit);

        // Two accounts, each in exactly one column — the expense holding a debit
        // balance and the cash account a credit one.
        Assert.Equal(2, trial.Rows.Count);

        TrialBalanceRow rent = trial.Rows.Single(r => r.AccountId == h.RentId);
        Assert.Equal(875.55m, rent.DebitBalance);
        Assert.Equal(0m, rent.CreditBalance);

        TrialBalanceRow cash = trial.Rows.Single(r => r.AccountId == h.CashId);
        Assert.Equal(0m, cash.DebitBalance);
        Assert.Equal(875.55m, cash.CreditBalance);
    }

    /// <summary>
    /// The opening balance is what makes a date-ranged ledger readable. Without
    /// it every running balance on the page is wrong by the same amount, which is
    /// the kind of wrong that looks entirely plausible.
    /// </summary>
    [SkippableFact]
    public async Task A_date_range_opens_at_what_came_before_it()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Post(2001, new DateOnly(2026, 6, 1), 300m, ct);
        await h.Post(2002, new DateOnly(2026, 7, 10), 100m, ct);
        await h.Post(2003, new DateOnly(2026, 7, 20), 50m, ct);

        AccountLedgerView? ledger = await h.Reports.GetAccountLedgerAsync(
            h.RentId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), ct);

        Assert.NotNull(ledger);
        Assert.Equal(300m, ledger.OpeningBalance);

        // Only the two inside the range.
        Assert.Equal(2, ledger.Rows.Count);
        Assert.Equal(150m, ledger.TotalDebit);
        Assert.Equal(0m, ledger.TotalCredit);

        // Carrying on from the opening figure rather than restarting at zero.
        Assert.Equal(400m, ledger.Rows[0].RunningBalance);
        Assert.Equal(450m, ledger.Rows[1].RunningBalance);
        Assert.Equal(450m, ledger.ClosingBalance);
    }

    /// <summary>
    /// Every row carries the document that wrote it. A ledger row that cannot be
    /// traced to its source is a number nobody can check.
    /// </summary>
    [SkippableFact]
    public async Task Every_row_names_the_document_behind_it()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Post(3001, new DateOnly(2026, 8, 1), 75m, ct);

        AccountLedgerView? ledger = await h.Reports.GetAccountLedgerAsync(h.RentId, null, null, ct);

        AccountLedgerRow row = Assert.Single(ledger!.Rows);

        Assert.Equal("JRN", row.TransactionTypeCode);
        Assert.Equal(3001, row.TransactionId);
        Assert.Equal(Control, row.LedgerTypeId);
        Assert.Equal("INR", row.CurrencyCode);
    }

    /// <summary>
    /// An account in another branch and an account that never existed look the
    /// same from outside. The query filter is what decides, so a caller cannot
    /// find out which by guessing an id.
    /// </summary>
    [SkippableFact]
    public async Task An_account_outside_the_branch_is_not_found()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        Assert.Null(await h.Reports.GetAccountLedgerAsync(
            h.RentId + 100_000, null, null, CancellationToken.None));
    }

    [SkippableFact]
    public async Task An_empty_ledger_balances_at_nothing()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        TrialBalanceView trial = await h.Reports.GetTrialBalanceAsync(
            null, null, CancellationToken.None);

        Assert.True(trial.IsBalanced);
        Assert.Empty(trial.Rows);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required LedgerPostingService Postings { get; init; }

        public required LedgerReportService Reports { get; init; }

        public required long RentId { get; init; }

        public required long CashId { get; init; }

        /// <summary>One balanced posting, through the door every service uses.</summary>
        public async Task Post(long transactionId, DateOnly on, decimal amount, CancellationToken ct)
        {
            PostLedgerResult result = await Postings.PostAsync(new PostLedgerRequest
            {
                TransactionTypeCode = "JRN",
                TransactionId = transactionId,
                LedgerSourceId = 12,
                LedgerDate = on,
                Legs =
                [
                    new LedgerLegRequest
                    {
                        LedgerTypeId = Control,
                        TransactionDetailId = 1,
                        AccountId = RentId,
                        DebitAmount = amount,
                    },
                    new LedgerLegRequest
                    {
                        LedgerTypeId = Control,
                        TransactionDetailId = 2,
                        AccountId = CashId,
                        CreditAmount = amount,
                    },
                ],
            }, ct);

            Assert.Equal(PostLedgerOutcome.Ok, result.Outcome);
        }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(
                tenant.CustomerId!.Value, tenant.OrgId!.Value);

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

            return new Harness
            {
                Db = db,
                Postings = new LedgerPostingService(db, tenant, new StubBaseCurrency()),
                Reports = new LedgerReportService(db),
                RentId = await Account("6100", "Rent", 5),
                CashId = await Account("1010", "Cash", 1),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

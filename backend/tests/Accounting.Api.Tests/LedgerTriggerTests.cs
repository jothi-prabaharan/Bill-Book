using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The database's half of the ledger's balance rule (TK-07).
///
/// The deferred constraint triggers on <c>acc.JournalLedger</c>,
/// <c>acc.JournalDetails</c> and <c>acc.Journals</c> were in the migration chain
/// squashed on 14 September 2026 and did not survive it, so for ten days the
/// only balance check left was the C# one on Post. These tests write straight
/// through the context, past every service, because the point is what the
/// database refuses on its own.
///
/// Each write runs in an explicit transaction and the refusal is asserted at
/// commit: the triggers are deferred, so the statement succeeds and the commit
/// is what fails. The allocation triggers on the money documents are covered by
/// <see cref="MoneyDocumentSchemaTests"/>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class LedgerTriggerTests
{
    private readonly PostgresFixture _postgres;

    public LedgerTriggerTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>The raise_exception state a PL/pgSQL <c>RAISE EXCEPTION</c> carries.</summary>
    private static void AssertRaised(Exception? error, string fragment)
    {
        Assert.NotNull(error);

        PostgresException? raised = null;
        for (Exception? e = error; e is not null; e = e.InnerException)
        {
            if (e is PostgresException pg)
            {
                raised = pg;
                break;
            }
        }

        Assert.NotNull(raised);
        Assert.Equal(PostgresErrorCodes.RaiseException, raised.SqlState);
        Assert.Contains(fragment, raised.MessageText);
    }

    private sealed class Branch : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required Guid OrgId { get; init; }

        public required long CashId { get; init; }

        public required long RentId { get; init; }

        public ValueTask DisposeAsync() => Db.DisposeAsync();

        public static async Task<Branch> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            AccountingDbContext db = postgres.CreateContext(Guid.NewGuid(), orgId);

            var cash = new Account { OrgId = orgId, AccountTypeId = 1, AccountCode = "1010", AccountName = "Cash", IsActive = true, IsJE = true };
            var rent = new Account { OrgId = orgId, AccountTypeId = 5, AccountCode = "6100", AccountName = "Rent", IsActive = true, IsJE = true };
            db.Accounts.AddRange(cash, rent);
            await db.SaveChangesAsync();

            return new Branch { Db = db, OrgId = orgId, CashId = cash.AccountId, RentId = rent.AccountId };
        }

        public Journal NewJournal(JournalStatus status) => new()
        {
            OrgId = OrgId,
            JournalDate = new DateOnly(2026, 8, 1),
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = status,
            JournalNo = status == JournalStatus.Draft ? null : $"JRN/{Guid.NewGuid():N}"[..30],
            PostedAt = status == JournalStatus.Draft ? null : DateTimeOffset.UtcNow,
        };

        public JournalDetail Line(Journal journal, int number, long accountId, decimal debit, decimal credit) => new()
        {
            OrgId = OrgId,
            JournalId = journal.JournalId,
            LineNumber = number,
            AccountId = accountId,
            DebitAmount = debit,
            CreditAmount = credit,
            DebitAmountBase = debit,
            CreditAmountBase = credit,
        };

        public JournalLedger Leg(long transactionId, int detail, long accountId, decimal debit, decimal credit) => new()
        {
            OrgId = OrgId,
            LedgerDate = new DateOnly(2026, 8, 1),
            AccountId = accountId,
            TransactionTypeCode = "JRN",
            TransactionId = transactionId,
            TransactionDetailId = detail,
            DebitAmount = debit,
            CreditAmount = credit,
            DebitAmountBase = debit,
            CreditAmountBase = credit,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            LedgerTypeId = 1,
            LedgerSourceId = 12,
        };

        /// <summary>Writes a journal and its lines in one transaction, and commits.</summary>
        public async Task WriteJournalAsync(Journal journal, params (long Account, decimal Debit, decimal Credit)[] lines)
        {
            await using IDbContextTransaction tx = await Db.Database.BeginTransactionAsync();

            Db.Journals.Add(journal);
            await Db.SaveChangesAsync();

            int n = 1;
            foreach ((long account, decimal debit, decimal credit) in lines)
            {
                Db.JournalDetails.Add(Line(journal, n++, account, debit, credit));
            }

            await Db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        /// <summary>Writes ledger rows in one transaction, and commits.</summary>
        public async Task WriteLegsAsync(params JournalLedger[] legs)
        {
            await using IDbContextTransaction tx = await Db.Database.BeginTransactionAsync();
            Db.JournalLedger.AddRange(legs);
            await Db.SaveChangesAsync();
            await tx.CommitAsync();
        }
    }

    // --- Journals ---

    [SkippableFact]
    public async Task A_posted_unbalanced_journal_cannot_be_committed()
    {
        await using Branch b = await Branch.CreateAsync(_postgres);

        Exception? error = await Record.ExceptionAsync(() => b.WriteJournalAsync(
            b.NewJournal(JournalStatus.Posted), (b.RentId, 100m, 0m), (b.CashId, 0m, 90m)));

        AssertRaised(error, "does not balance");
    }

    [SkippableFact]
    public async Task A_draft_may_be_unbalanced()
    {
        await using Branch b = await Branch.CreateAsync(_postgres);
        Journal draft = b.NewJournal(JournalStatus.Draft);

        await b.WriteJournalAsync(draft, (b.RentId, 100m, 0m), (b.CashId, 0m, 90m));

        Assert.Equal(2, await b.Db.JournalDetails.CountAsync(d => d.JournalId == draft.JournalId));
    }

    [SkippableFact]
    public async Task A_posted_balanced_journal_commits()
    {
        await using Branch b = await Branch.CreateAsync(_postgres);
        Journal journal = b.NewJournal(JournalStatus.Posted);

        await b.WriteJournalAsync(journal, (b.RentId, 100m, 0m), (b.CashId, 0m, 100m));

        Assert.Equal(2, await b.Db.JournalDetails.CountAsync(d => d.JournalId == journal.JournalId));
    }

    /// <summary>
    /// Posting a draft touches only the header, so the trigger on the lines never
    /// fires for it. The header's own trigger is what catches this.
    /// </summary>
    [SkippableFact]
    public async Task Posting_an_unbalanced_draft_is_refused_at_commit()
    {
        await using Branch b = await Branch.CreateAsync(_postgres);
        Journal draft = b.NewJournal(JournalStatus.Draft);
        await b.WriteJournalAsync(draft, (b.RentId, 100m, 0m), (b.CashId, 0m, 90m));

        Exception? error = await Record.ExceptionAsync(async () =>
        {
            await using IDbContextTransaction tx = await b.Db.Database.BeginTransactionAsync();
            draft.Status = JournalStatus.Posted;
            draft.JournalNo = $"JRN/{draft.JournalId}";
            draft.PostedAt = DateTimeOffset.UtcNow;
            await b.Db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        AssertRaised(error, "does not balance");
    }

    [SkippableFact]
    public async Task A_posted_journals_line_cannot_be_edited_out_of_balance()
    {
        await using Branch b = await Branch.CreateAsync(_postgres);
        Journal journal = b.NewJournal(JournalStatus.Posted);
        await b.WriteJournalAsync(journal, (b.RentId, 100m, 0m), (b.CashId, 0m, 100m));

        JournalDetail debit = await b.Db.JournalDetails
            .SingleAsync(d => d.JournalId == journal.JournalId && d.DebitAmount > 0);

        Exception? error = await Record.ExceptionAsync(async () =>
        {
            await using IDbContextTransaction tx = await b.Db.Database.BeginTransactionAsync();
            debit.DebitAmount = 80m;
            debit.DebitAmountBase = 80m;
            await b.Db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        AssertRaised(error, "does not balance");
    }

    // --- The branch ledger ---

    [SkippableFact]
    public async Task Unbalanced_ledger_rows_cannot_be_committed()
    {
        await using Branch b = await Branch.CreateAsync(_postgres);

        Exception? error = await Record.ExceptionAsync(() => b.WriteLegsAsync(
            b.Leg(1, 1, b.RentId, 100m, 0m),
            b.Leg(1, 2, b.CashId, 0m, 99.99m)));

        AssertRaised(error, "branch ledger does not balance");
    }

    [SkippableFact]
    public async Task Balanced_ledger_rows_commit()
    {
        await using Branch b = await Branch.CreateAsync(_postgres);

        await b.WriteLegsAsync(
            b.Leg(1, 1, b.RentId, 100m, 0m),
            b.Leg(1, 2, b.CashId, 0m, 100m));

        Assert.Equal(2, await b.Db.JournalLedger.CountAsync(l => l.TransactionId == 1));
    }

    /// <summary>
    /// The pre-squash trigger read <c>NEW."OrgId"</c> on a delete, where NEW is
    /// null, so it summed no rows and passed: taking one leg out of a posting
    /// went unchecked. It reads OLD on a delete now.
    /// </summary>
    [SkippableFact]
    public async Task Deleting_one_leg_of_a_posting_is_refused()
    {
        await using Branch b = await Branch.CreateAsync(_postgres);
        JournalLedger debit = b.Leg(1, 1, b.RentId, 100m, 0m);
        await b.WriteLegsAsync(debit, b.Leg(1, 2, b.CashId, 0m, 100m));

        Exception? error = await Record.ExceptionAsync(async () =>
        {
            await using IDbContextTransaction tx = await b.Db.Database.BeginTransactionAsync();
            b.Db.JournalLedger.Remove(debit);
            await b.Db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        AssertRaised(error, "branch ledger does not balance");
    }

    /// <summary>
    /// One check per branch per transaction, not one per row: a second posting
    /// in the same transaction that unbalances the branch must still be caught,
    /// because every deferred check runs after the last statement.
    /// </summary>
    [SkippableFact]
    public async Task Two_postings_in_one_transaction_are_checked_together()
    {
        await using Branch b = await Branch.CreateAsync(_postgres);

        Exception? error = await Record.ExceptionAsync(async () =>
        {
            await using IDbContextTransaction tx = await b.Db.Database.BeginTransactionAsync();

            b.Db.JournalLedger.AddRange(b.Leg(1, 1, b.RentId, 100m, 0m), b.Leg(1, 2, b.CashId, 0m, 100m));
            await b.Db.SaveChangesAsync();

            b.Db.JournalLedger.Add(b.Leg(2, 1, b.RentId, 5m, 0m));
            await b.Db.SaveChangesAsync();

            await tx.CommitAsync();
        });

        AssertRaised(error, "branch ledger does not balance");
    }

    // --- The catalogue ---

    /// <summary>
    /// Every trigger exists and is deferred. An immediate one would refuse the
    /// first line of every multi-line posting, and a missing one is the state
    /// the squash left behind without anything noticing.
    /// </summary>
    [SkippableFact]
    public async Task Every_balance_trigger_exists_and_is_deferred()
    {
        await using Branch b = await Branch.CreateAsync(_postgres);

        string[] expected =
        [
            "trg_journal_balanced",
            "trg_journal_balanced_on_post",
            "trg_ledger_balanced",
            "trg_receivemoney_allocated",
            "trg_receivemoney_allocated_on_post",
            "trg_spendmoney_allocated",
            "trg_spendmoney_allocated_on_post",
        ];

        List<string> deferred = await b.Db.Database
            .SqlQueryRaw<string>(
                """
                SELECT t.tgname AS "Value"
                  FROM pg_trigger t
                  JOIN pg_class c ON c.oid = t.tgrelid
                  JOIN pg_namespace n ON n.oid = c.relnamespace
                 WHERE n.nspname = 'acc'
                   AND NOT t.tgisinternal
                   AND t.tgdeferrable
                   AND t.tginitdeferred
                """)
            .ToListAsync();

        Assert.All(expected, name => Assert.Contains(name, deferred));
    }
}

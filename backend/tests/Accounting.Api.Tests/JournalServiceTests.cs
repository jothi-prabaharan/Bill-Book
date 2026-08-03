using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The manual journal, end to end against a real database — because most of what
/// makes it correct is the database's half: the deferred balance trigger, the
/// guarded number allocation, and the transaction that ties the two to the
/// ledger rows.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class JournalServiceTests
{
    private const int JournalSource = 12;

    private readonly PostgresFixture _postgres;

    public JournalServiceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>A draft may be unbalanced. That is what a draft is for.</summary>
    [SkippableFact]
    public async Task An_unbalanced_draft_saves()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        SaveJournalResult result = await h.Journals.CreateAsync(
            Entry(h, debit: 500m, credit: 300m), CancellationToken.None);

        Assert.Equal(SaveJournalOutcome.Ok, result.Outcome);

        JournalDetailView? saved = await h.Journals.GetAsync(result.JournalId, CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal("Draft", saved.Status);

        // No number while it is a draft: a number taken at draft is a number lost
        // every time someone changes their mind, and this series has to be gapless.
        Assert.Null(saved.JournalNo);
        Assert.Equal(500m, saved.TotalDebit);
        Assert.Equal(300m, saved.TotalCredit);
    }

    [SkippableFact]
    public async Task An_unbalanced_entry_cannot_be_posted()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        SaveJournalResult draft = await h.Journals.CreateAsync(Entry(h, 500m, 300m), ct);
        SaveJournalResult posted = await h.Journals.PostAsync(draft.JournalId, ct);

        Assert.Equal(SaveJournalOutcome.Unbalanced, posted.Outcome);
        Assert.Contains("200", posted.Detail);

        // And nothing moved: still a draft, still no number, no ledger rows.
        JournalDetailView? after = await h.Journals.GetAsync(draft.JournalId, ct);
        Assert.Equal("Draft", after!.Status);
        Assert.Null(after.JournalNo);
        Assert.Empty(await h.LedgerRowsFor(draft.JournalId, ct));
    }

    /// <summary>
    /// The T1.1 "done when": a balanced entry posts, takes its number, and the
    /// ledger rows that come out of it are the ones the entry described.
    /// </summary>
    [SkippableFact]
    public async Task Posting_a_balanced_entry_writes_the_ledger_rows()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        SaveJournalResult draft = await h.Journals.CreateAsync(Entry(h, 500m, 500m), ct);
        SaveJournalResult posted = await h.Journals.PostAsync(draft.JournalId, ct);

        Assert.Equal(SaveJournalOutcome.Ok, posted.Outcome);

        JournalDetailView? after = await h.Journals.GetAsync(draft.JournalId, ct);
        Assert.Equal("Posted", after!.Status);
        Assert.StartsWith("JV/", after.JournalNo);
        Assert.NotNull(after.PostedAt);

        List<JournalLedger> rows = await h.LedgerRowsFor(draft.JournalId, ct);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("JRN", r.TransactionTypeCode));
        Assert.All(rows, r => Assert.Equal(JournalSource, r.LedgerSourceId));

        // The journal id on every row, which is what lets a ledger line drill
        // back to the entry rather than only to a type and a number.
        Assert.All(rows, r => Assert.Equal(draft.JournalId, r.JournalId));

        Assert.Equal(500m, rows.Sum(r => r.DebitAmountBase));
        Assert.Equal(500m, rows.Sum(r => r.CreditAmountBase));
    }

    [SkippableFact]
    public async Task A_posted_entry_refuses_an_edit_and_a_delete()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        SaveJournalResult draft = await h.Journals.CreateAsync(Entry(h, 500m, 500m), ct);
        await h.Journals.PostAsync(draft.JournalId, ct);

        Assert.Equal(
            SaveJournalOutcome.NotDraft,
            (await h.Journals.UpdateAsync(draft.JournalId, Entry(h, 10m, 10m), ct)).Outcome);

        Assert.Equal(
            SaveJournalOutcome.NotDraft,
            (await h.Journals.DeleteAsync(draft.JournalId, ct)).Outcome);
    }

    /// <summary>
    /// The reversal: both headers linked, every line linked to the line it
    /// offsets, and the ledger left at nothing for that pair of accounts.
    /// </summary>
    [SkippableFact]
    public async Task A_reversal_offsets_the_entry_exactly_and_pairs_every_line()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        SaveJournalResult draft = await h.Journals.CreateAsync(Entry(h, 500m, 500m), ct);
        await h.Journals.PostAsync(draft.JournalId, ct);

        SaveJournalResult reversed = await h.Journals.ReverseAsync(
            draft.JournalId, new ReverseJournalRequest(), ct);

        Assert.Equal(SaveJournalOutcome.Ok, reversed.Outcome);
        Assert.NotEqual(draft.JournalId, reversed.JournalId);

        JournalDetailView? original = await h.Journals.GetAsync(draft.JournalId, ct);
        JournalDetailView? reversal = await h.Journals.GetAsync(reversed.JournalId, ct);

        // Both headers point at each other.
        Assert.Equal("Reversed", original!.Status);
        Assert.Equal(reversed.JournalId, original.ReversedByJournalId);
        Assert.Equal(draft.JournalId, reversal!.ReversesJournalId);
        Assert.Equal(original.JournalNo, reversal.ReversesJournalNo);

        // And so does every line. Without this, a reversal that missed a line
        // would still balance and still look complete at the header.
        Assert.Equal(original.Lines.Count, reversal.Lines.Count);

        foreach (JournalLineView line in original.Lines)
        {
            JournalLineView offset = Assert.Single(
                reversal.Lines, r => r.ReversesJournalDetailId == line.JournalDetailId);

            Assert.Equal(offset.JournalDetailId, line.ReversedByJournalDetailId);

            // Sides swapped, amounts identical — an offsetting entry, never a
            // negative one.
            Assert.Equal(line.DebitAmount, offset.CreditAmount);
            Assert.Equal(line.CreditAmount, offset.DebitAmount);
        }

        // The pair nets to nothing on every account it touched.
        List<JournalLedger> both = await h.Db.JournalLedger
            .Where(l => l.TransactionTypeCode == "JRN")
            .ToListAsync(ct);

        Assert.Equal(4, both.Count);

        foreach (IGrouping<long, JournalLedger> account in both.GroupBy(l => l.AccountId))
        {
            Assert.Equal(
                account.Sum(l => l.DebitAmountBase),
                account.Sum(l => l.CreditAmountBase));
        }
    }

    [SkippableFact]
    public async Task An_entry_cannot_be_reversed_twice()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        SaveJournalResult draft = await h.Journals.CreateAsync(Entry(h, 500m, 500m), ct);
        await h.Journals.PostAsync(draft.JournalId, ct);
        await h.Journals.ReverseAsync(draft.JournalId, new ReverseJournalRequest(), ct);

        SaveJournalResult again = await h.Journals.ReverseAsync(
            draft.JournalId, new ReverseJournalRequest(), ct);

        Assert.Equal(SaveJournalOutcome.AlreadyReversed, again.Outcome);
    }

    [SkippableFact]
    public async Task A_draft_cannot_be_reversed()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        SaveJournalResult draft = await h.Journals.CreateAsync(Entry(h, 500m, 500m), ct);

        Assert.Equal(
            SaveJournalOutcome.NotPosted,
            (await h.Journals.ReverseAsync(draft.JournalId, new ReverseJournalRequest(), ct)).Outcome);
    }

    [SkippableFact]
    public async Task A_line_that_is_both_a_debit_and_a_credit_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        SaveJournalRequest request = Entry(h, 500m, 500m);
        request.Lines[0].CreditAmount = 500m;

        Assert.Equal(
            SaveJournalOutcome.LineNotExclusive,
            (await h.Journals.CreateAsync(request, CancellationToken.None)).Outcome);
    }

    /// <summary>
    /// A seeded control account is driven by its own subledger, so a hand entry
    /// against one puts the two out of step with nothing to reconcile them.
    /// </summary>
    [SkippableFact]
    public async Task A_control_account_not_open_to_journals_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        SaveJournalRequest request = Entry(h, 500m, 500m);
        request.Lines[0].AccountId = h.ReceivableId;

        SaveJournalResult result = await h.Journals.CreateAsync(request, CancellationToken.None);

        Assert.Equal(SaveJournalOutcome.AccountNotPostable, result.Outcome);
        Assert.Contains("subledger", result.Detail);
    }

    [SkippableFact]
    public async Task A_draft_can_be_edited_and_deleted()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        SaveJournalResult draft = await h.Journals.CreateAsync(Entry(h, 500m, 300m), ct);

        Assert.Equal(
            SaveJournalOutcome.Ok,
            (await h.Journals.UpdateAsync(draft.JournalId, Entry(h, 900m, 900m), ct)).Outcome);

        JournalDetailView? edited = await h.Journals.GetAsync(draft.JournalId, ct);
        Assert.Equal(900m, edited!.TotalDebit);
        Assert.Equal(900m, edited.TotalCredit);

        Assert.Equal(
            SaveJournalOutcome.Ok,
            (await h.Journals.DeleteAsync(draft.JournalId, ct)).Outcome);

        Assert.Null(await h.Journals.GetAsync(draft.JournalId, ct));
    }

    /// <summary>
    /// A failed post gives its number back. The series has to be gapless, and a
    /// number consumed by an entry that never posted is a gap an auditor asks
    /// about.
    /// </summary>
    [SkippableFact]
    public async Task A_refused_post_leaves_the_numbering_series_where_it_was()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long before = await h.NextNumber(ct);

        SaveJournalResult draft = await h.Journals.CreateAsync(Entry(h, 500m, 300m), ct);
        await h.Journals.PostAsync(draft.JournalId, ct);

        Assert.Equal(before, await h.NextNumber(ct));
    }

    /// <summary>Two entries posted one after the other take consecutive numbers.</summary>
    [SkippableFact]
    public async Task Posted_entries_take_consecutive_numbers()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        SaveJournalResult first = await h.Journals.CreateAsync(Entry(h, 100m, 100m), ct);
        SaveJournalResult second = await h.Journals.CreateAsync(Entry(h, 200m, 200m), ct);

        await h.Journals.PostAsync(first.JournalId, ct);
        await h.Journals.PostAsync(second.JournalId, ct);

        Assert.Equal("JV/2627/00001", (await h.Journals.GetAsync(first.JournalId, ct))!.JournalNo);
        Assert.Equal("JV/2627/00002", (await h.Journals.GetAsync(second.JournalId, ct))!.JournalNo);
    }

    private static SaveJournalRequest Entry(Harness h, decimal debit, decimal credit) => new()
    {
        JournalDate = new DateOnly(2026, 8, 1),
        Reference = "Test entry",
        Lines =
        [
            new SaveJournalLineRequest { AccountId = h.RentId, DebitAmount = debit },
            new SaveJournalLineRequest { AccountId = h.CashId, CreditAmount = credit },
        ],
    };

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required JournalService Journals { get; init; }

        public required long RentId { get; init; }

        public required long CashId { get; init; }

        /// <summary>A seeded control account with IsJE off — not open to hand entries.</summary>
        public required long ReceivableId { get; init; }

        public Task<List<JournalLedger>> LedgerRowsFor(long journalId, CancellationToken ct) =>
            Db.JournalLedger.Where(l => l.JournalId == journalId).ToListAsync(ct);

        public async Task<long> NextNumber(CancellationToken ct) =>
            await Db.NumberingSeries
                .Where(s => s.SeriesCode == "JRN")
                .Select(s => s.NextNumber)
                .FirstAsync(ct);

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(
                tenant.CustomerId!.Value, tenant.OrgId!.Value);

            async Task<long> Account(
                string code, string name, int typeId, bool systemDefault = false, bool isJe = false)
            {
                var account = new Account
                {
                    OrgId = orgId,
                    AccountTypeId = typeId,
                    AccountCode = code,
                    AccountName = name,
                    IsActive = true,
                    IsSystemDefault = systemDefault,
                    IsJE = isJe,
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            // The branch's own JRN series, exactly as the org seed writes it.
            db.NumberingSeries.AddRange(
                Repository.SeedData.NumberingSeriesSeed.Build(orgId).Where(s => s.SeriesCode == "JRN"));
            await db.SaveChangesAsync();

            var numbers = new NumberGenerator(
                db,
                Options.Create(new NumberingOptions()),
                new StubFinancialYear());

            var postings = new LedgerPostingService(db, tenant, new StubBaseCurrency());

            return new Harness
            {
                Db = db,
                RentId = await Account("6100", "Rent", 5),
                CashId = await Account("1010", "Cash", 1),
                ReceivableId = await Account(
                    "1100", "Accounts Receivable", 1, systemDefault: true, isJe: false),
                Journals = new JournalService(
                    db,
                    postings,
                    new PeriodLockService(db, new StubCurrentUser()),
                    numbers,
                    new StubBaseCurrency(),
                    new StubCurrentUser(),
                    tenant,
                    TimeProvider.System),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

using Accounting.Api.Services;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The allocation guard, against a real database.
///
/// What is being proved is T5.1: a claim on a document must never exceed what
/// the document still represents, and the sum spans rows — the target's CONTROL
/// legs say what it was worth, every existing allocation says what has already
/// been claimed, and only the difference is available. The guard reads both and
/// writes its row in one serializable transaction, so two allocations racing
/// each other cannot both pass a guard neither saw the other's row.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AllocationServiceTests
{
    private const int Item = 1;
    private const int Control = 3;

    /// <summary>`mst.LedgerSources` 1 — a document posting.</summary>
    private const int DocumentPosting = 1;

    private readonly PostgresFixture _postgres;

    public AllocationServiceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// The happy path, and the row it must leave behind: source and target
    /// named, so a void can remove exactly the claims its document made.
    /// </summary>
    [SkippableFact]
    public async Task An_allocation_within_what_the_invoice_still_represents_succeeds()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5001, 1000m, ct);

        AllocationResult result = await harness.Allocations.AllocateAsync(
            Request("CRN", 9001, "INV", 5001, 700m), ct);

        Assert.Equal(AllocationOutcome.Ok, result.Outcome);

        TransactionRatio row = await harness.Db.TransactionRatios.SingleAsync(ct);
        Assert.Equal("CRN", row.SourceTransactionTypeCode);
        Assert.Equal(9001, row.SourceTransactionId);
        Assert.Equal("INV", row.TargetTransactionTypeCode);
        Assert.Equal(5001, row.TargetTransactionId);
        Assert.Equal(700m, row.Amount);
    }

    /// <summary>
    /// The whole point of the guard: a claim that would leave the target owing
    /// more than it ever represented is refused, with the figures in the message
    /// rather than a bare failure.
    /// </summary>
    [SkippableFact]
    public async Task An_allocation_that_exceeds_the_invoice_is_refused()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5002, 1000m, ct);

        AllocationResult result = await harness.Allocations.AllocateAsync(
            Request("CRN", 9002, "INV", 5002, 1000.01m), ct);

        Assert.Equal(AllocationOutcome.Refused, result.Outcome);
        Assert.Contains("exceed", result.Message);
        Assert.Empty(await harness.Db.TransactionRatios.ToListAsync(ct));
    }

    /// <summary>
    /// The sum spans rows: a second allocation is judged against what the first
    /// already claimed, not against the invoice alone.
    /// </summary>
    [SkippableFact]
    public async Task Two_allocations_are_judged_together()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5003, 1000m, ct);

        Assert.Equal(
            AllocationOutcome.Ok,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9101, "INV", 5003, 700m), ct)).Outcome);

        // 300 remains, so a further 500 would over-claim…
        AllocationResult second = await harness.Allocations.AllocateAsync(
            Request("CRN", 9102, "INV", 5003, 500m), ct);
        Assert.Equal(AllocationOutcome.Refused, second.Outcome);

        // …and 300 is exactly what is left.
        Assert.Equal(
            AllocationOutcome.Ok,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9103, "INV", 5003, 300m), ct)).Outcome);

        Assert.Equal(
            1000m,
            await harness.Db.TransactionRatios.SumAsync(t => t.Amount, ct));
    }

    /// <summary>
    /// Replace, never append: a repost after a dropped response lands one row,
    /// and is judged against the other claims rather than against itself.
    /// </summary>
    [SkippableFact]
    public async Task Re_allocating_the_same_pair_replaces_instead_of_doubling()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5004, 1000m, ct);

        Assert.Equal(
            AllocationOutcome.Ok,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9201, "INV", 5004, 700m), ct)).Outcome);

        // The pair is excluded from its own judgement, so the retry passes…
        Assert.Equal(
            AllocationOutcome.Ok,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9201, "INV", 5004, 700m), ct)).Outcome);

        // …and leaves one row, not two.
        TransactionRatio row = Assert.Single(await harness.Db.TransactionRatios.ToListAsync(ct));
        Assert.Equal(700m, row.Amount);
    }

    /// <summary>
    /// A document with nothing outstanding owes nothing. The guard reads the
    /// ledger rather than trusting a caller's figure, so it answers "no" to a
    /// claim on a document that was never posted — or whose control legs net
    /// to zero.
    /// </summary>
    [SkippableFact]
    public async Task A_claim_on_a_document_with_nothing_outstanding_is_refused()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        // Never posted. There are no control legs to read.
        AllocationResult result = await harness.Allocations.AllocateAsync(
            Request("CRN", 9301, "INV", 5099, 100m), ct);

        Assert.Equal(AllocationOutcome.Refused, result.Outcome);
        Assert.Contains("no outstanding", result.Message);
    }

    /// <summary>
    /// The check constraint would refuse the row anyway; the guard says why
    /// before the database has to. A zero or negative claim is nonsense either
    /// way.
    /// </summary>
    [SkippableFact]
    public async Task A_zero_or_negative_allocation_is_refused()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5006, 1000m, ct);

        Assert.Equal(
            AllocationOutcome.Refused,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9401, "INV", 5006, 0m), ct)).Outcome);
        Assert.Equal(
            AllocationOutcome.Refused,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9402, "INV", 5006, -100m), ct)).Outcome);
        Assert.Empty(await harness.Db.TransactionRatios.ToListAsync(ct));
    }

    /// <summary>
    /// The guard reads the CONTROL net, not the gross: a credit under the
    /// document's own key reduces what it still represents, and a claim against
    /// the gross would hand out money the document no longer owes.
    /// </summary>
    [SkippableFact]
    public async Task The_guard_reads_the_control_net_not_the_gross()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5007, 1000m, ct);

        // A correction under the same key: ₹300 of the receivable is gone. Both
        // correction legs are on fresh lines, so nothing of the invoice is
        // replaced — the replace key is (type, id, leg type, detail), and a
        // credit on the invoice's own lines would take its rows' place instead
        // of offsetting them.
        await harness.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = 5007,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(Control, 2, harness.ReceivableId, credit: 300m),
                Leg(Item, 2, harness.RevenueId, debit: 300m),
            ],
        }, ct);

        Assert.Equal(
            AllocationOutcome.Refused,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9501, "INV", 5007, 800m), ct)).Outcome);

        Assert.Equal(
            AllocationOutcome.Ok,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9502, "INV", 5007, 700m), ct)).Outcome);
    }

    /// <summary>
    /// The query filter is load-bearing here: one branch's claims must not
    /// reduce another branch's available balance. Both branches post an invoice
    /// with the same id — same database, different books.
    /// </summary>
    [SkippableFact]
    public async Task One_organizations_allocations_do_not_count_against_anothers()
    {
        CancellationToken ct = CancellationToken.None;

        await using Harness first = await Harness.CreateAsync(_postgres);
        await using Harness second = await Harness.CreateAsync(_postgres);

        await first.PostInvoiceAsync(5008, 1000m, ct);
        await second.PostInvoiceAsync(5008, 1000m, ct);

        Assert.Equal(
            AllocationOutcome.Ok,
            (await first.Allocations.AllocateAsync(Request("CRN", 9601, "INV", 5008, 700m), ct)).Outcome);

        // The second branch's invoice still has its full thousand: the first
        // branch's claim is invisible here.
        Assert.Equal(
            AllocationOutcome.Ok,
            (await second.Allocations.AllocateAsync(Request("CRN", 9602, "INV", 5008, 1000m), ct)).Outcome);

        // Each branch's books hold exactly its own claim, and nothing of the
        // other's.
        Assert.Equal(1, await first.Db.TransactionRatios.CountAsync(ct));
        Assert.Equal(1, await second.Db.TransactionRatios.CountAsync(ct));
    }

    /// <summary>
    /// A void releases the source document's claims and no others — and the
    /// release is real: a claim that no longer holds must not keep occupying the
    /// target's balance.
    ///
    /// <b>The row stays.</b> A released allocation is history rather than an
    /// absence, so what was claimed against an invoice before a credit note was
    /// withdrawn stays answerable. The release is carried by the guard ignoring
    /// voided rows, which is what the last assertion actually proves.
    /// </summary>
    [SkippableFact]
    public async Task Removing_a_source_releases_its_claims_and_no_others()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5009, 1000m, ct);

        await harness.Allocations.AllocateAsync(Request("CRN", 9701, "INV", 5009, 300m), ct);
        await harness.Allocations.AllocateAsync(Request("CRN", 9702, "INV", 5009, 300m), ct);

        await harness.Allocations.RemoveAllocationsAsync("CRN", 9701, ct);

        // The release went straight to the database, so the rows this context
        // still tracks from the allocations above are stale.
        harness.Db.ChangeTracker.Clear();

        List<TransactionRatio> rows = await harness.Db.TransactionRatios
            .OrderBy(t => t.SourceTransactionId)
            .ToListAsync(ct);

        Assert.Equal(2, rows.Count);

        TransactionRatio released = rows[0];
        Assert.Equal(9701, released.SourceTransactionId);
        Assert.True(released.IsVoided);
        Assert.NotNull(released.VoidedAt);
        Assert.False(string.IsNullOrWhiteSpace(released.VoidReason));

        TransactionRatio kept = rows[1];
        Assert.Equal(9702, kept.SourceTransactionId);
        Assert.False(kept.IsVoided);

        // 300 was released; 400 was still free. 700 is now exactly available —
        // which only holds if the voided row stopped counting against the target.
        Assert.Equal(
            AllocationOutcome.Ok,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9703, "INV", 5009, 700m), ct)).Outcome);
    }

    /// <summary>
    /// Voiding one allocation by its id returns exactly that claim, leaving
    /// every other claim on the same target alone.
    /// </summary>
    [SkippableFact]
    public async Task Voiding_one_allocation_by_id_releases_only_that_claim()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5010, 1000m, ct);

        await harness.Allocations.AllocateAsync(Request("CRN", 9801, "INV", 5010, 400m), ct);
        await harness.Allocations.AllocateAsync(Request("CRN", 9802, "INV", 5010, 400m), ct);

        long id = await harness.Db.TransactionRatios
            .Where(t => t.SourceTransactionId == 9801)
            .Select(t => t.TransactionRatioId)
            .SingleAsync(ct);

        Assert.True(await harness.Allocations.VoidAsync(id, "Keyed against the wrong invoice.", ct));

        harness.Db.ChangeTracker.Clear();

        TransactionRatio voided = await harness.Db.TransactionRatios
            .SingleAsync(t => t.TransactionRatioId == id, ct);

        Assert.True(voided.IsVoided);
        Assert.Equal("Keyed against the wrong invoice.", voided.VoidReason);

        // 400 came back and 200 was never claimed, so 600 is free — and the
        // other note's 400 is still held.
        Assert.Equal(
            AllocationOutcome.Ok,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9803, "INV", 5010, 600m), ct)).Outcome);
    }

    /// <summary>
    /// Voiding the same allocation twice is refused rather than silently
    /// releasing nothing — the caller is told it was already void.
    /// </summary>
    [SkippableFact]
    public async Task Voiding_an_already_voided_allocation_reports_that_it_did_nothing()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5011, 500m, ct);
        await harness.Allocations.AllocateAsync(Request("CRN", 9901, "INV", 5011, 100m), ct);

        long id = await harness.Db.TransactionRatios
            .Select(t => t.TransactionRatioId)
            .SingleAsync(ct);

        Assert.True(await harness.Allocations.VoidAsync(id, "First.", ct));
        Assert.False(await harness.Allocations.VoidAsync(id, "Second.", ct));

        harness.Db.ChangeTracker.Clear();

        // The first reason stands: a second void must not overwrite the record
        // of why the claim was actually released.
        TransactionRatio row = await harness.Db.TransactionRatios.SingleAsync(ct);
        Assert.Equal("First.", row.VoidReason);
    }

    /// <summary>
    /// The open-documents workspace, split by the direction the balance runs: an
    /// invoice is something to settle, an advance is credit to settle it with,
    /// and what is already claimed comes off both.
    /// </summary>
    [SkippableFact]
    public async Task Open_documents_split_targets_from_credits_and_net_off_live_claims()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5100, 1000m, ct, contactId: 42);
        await harness.PostCreditAsync(9100, 250m, ct, contactId: 42);

        await harness.Allocations.AllocateAsync(Request("CRN", 9100, "INV", 5100, 100m), ct);

        OpenDocumentsDto open = await harness.Allocations.GetOpenDocumentsAsync(42, ct);

        OpenDocumentDto target = Assert.Single(open.Targets);
        Assert.Equal("INV", target.TransactionTypeCode);
        Assert.Equal(1000m, target.TotalAmount);
        Assert.Equal(100m, target.AllocatedAmount);
        Assert.Equal(900m, target.UnallocatedAmount);
        Assert.Equal(SettlementStatus.PartiallyPaid, target.SettlementStatus);

        OpenDocumentDto source = Assert.Single(open.Sources);
        Assert.Equal("CRN", source.TransactionTypeCode);
        Assert.Equal(250m, source.TotalAmount);
        Assert.Equal(100m, source.AllocatedAmount);
        Assert.Equal(150m, source.UnallocatedAmount);

        Assert.Equal(900m, open.TotalOutstanding);
        Assert.Equal(150m, open.TotalAvailableCredit);
    }

    /// <summary>
    /// A document with nothing left to claim drops out of the workspace rather
    /// than showing as a row nothing can be done with.
    /// </summary>
    [SkippableFact]
    public async Task A_fully_allocated_document_leaves_the_workspace()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5200, 300m, ct, contactId: 77);
        await harness.PostCreditAsync(9200, 300m, ct, contactId: 77);

        await harness.Allocations.AllocateAsync(Request("CRN", 9200, "INV", 5200, 300m), ct);

        OpenDocumentsDto open = await harness.Allocations.GetOpenDocumentsAsync(77, ct);

        Assert.Empty(open.Targets);
        Assert.Empty(open.Sources);
        Assert.Equal(0m, open.TotalOutstanding);
    }

    /// <summary>
    /// The list counts what matched rather than what fitted on the page, and
    /// leaves voided rows out unless they are asked for.
    /// </summary>
    [SkippableFact]
    public async Task The_list_pages_and_hides_voided_rows_unless_asked()
    {
        await using Harness harness = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await harness.PostInvoiceAsync(5300, 1000m, ct);

        for (long note = 9301; note <= 9303; note++)
        {
            await harness.Allocations.AllocateAsync(Request("CRN", note, "INV", 5300, 100m), ct);
        }

        long first = await harness.Db.TransactionRatios
            .Where(t => t.SourceTransactionId == 9301)
            .Select(t => t.TransactionRatioId)
            .SingleAsync(ct);

        await harness.Allocations.VoidAsync(first, "Withdrawn.", ct);

        AllocationPageDto live = await harness.Allocations.ListAsync(1, 2, null, false, ct);
        Assert.Equal(2, live.TotalCount);
        Assert.Equal(2, live.Items.Count);
        Assert.DoesNotContain(live.Items, i => i.IsVoided);

        AllocationPageDto all = await harness.Allocations.ListAsync(1, 2, null, true, ct);
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(2, all.Items.Count);

        // An oversized page size is clamped rather than trusted.
        AllocationPageDto clamped = await harness.Allocations.ListAsync(1, 100_000, null, true, ct);
        Assert.Equal(200, clamped.PageSize);
    }

    private static AllocateTransactionRequest Request(
        string sourceCode, long sourceId, string targetCode, long targetId, decimal amount) =>
        new()
        {
            SourceTransactionTypeCode = sourceCode,
            SourceTransactionId = sourceId,
            TargetTransactionTypeCode = targetCode,
            TargetTransactionId = targetId,
            Amount = amount,
        };

    private static LedgerLegRequest Leg(
        int ledgerTypeId, long detailId, long accountId, decimal debit = 0m, decimal credit = 0m) =>
        new()
        {
            LedgerTypeId = ledgerTypeId,
            LedgerSourceId = DocumentPosting,
            TransactionDetailId = detailId,
            AccountId = accountId,
            DebitAmount = debit,
            CreditAmount = credit,
        };

    /// <summary>
    /// One branch with the two accounts these tests post to. A fresh OrgId per
    /// test, so the query filter keeps them from seeing each other.
    /// </summary>
    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required LedgerPostingService Postings { get; init; }

        public required AllocationService Allocations { get; init; }

        public required long RevenueId { get; init; }

        public required long ReceivableId { get; init; }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(tenant.CustomerId.Value, orgId);

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
                Allocations = new AllocationService(
                    db, tenant, NullLogger<AllocationService>.Instance),
                RevenueId = await Account("4100", "Sales Revenue", 4),
                ReceivableId = await Account("1100", "Accounts Receivable", 1),
            };
        }

        /// <summary>An invoice worth <paramref name="amount"/>: revenue against a receivable.</summary>
        public Task PostInvoiceAsync(
            long invoiceId, decimal amount, CancellationToken ct, long? contactId = null) =>
            Postings.PostAsync(new PostLedgerRequest
            {
                TransactionTypeCode = "INV",
                TransactionId = invoiceId,
                LedgerDate = new DateOnly(2026, 8, 1),
                ContactId = contactId,
                Legs =
                [
                    Leg(Item, 1, RevenueId, credit: amount),
                    Leg(Control, 0, ReceivableId, debit: amount),
                ],
            }, ct);

        /// <summary>
        /// A credit note worth <paramref name="amount"/> — the mirror of the
        /// invoice, so its CONTROL net runs the other way and it lands on the
        /// source side of the workspace.
        /// </summary>
        public Task PostCreditAsync(
            long creditNoteId, decimal amount, CancellationToken ct, long? contactId = null) =>
            Postings.PostAsync(new PostLedgerRequest
            {
                TransactionTypeCode = "CRN",
                TransactionId = creditNoteId,
                LedgerDate = new DateOnly(2026, 8, 2),
                ContactId = contactId,
                Legs =
                [
                    Leg(Item, 1, RevenueId, debit: amount),
                    Leg(Control, 0, ReceivableId, credit: amount),
                ],
            }, ct);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
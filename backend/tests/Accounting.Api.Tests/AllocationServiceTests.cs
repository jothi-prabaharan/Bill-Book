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
    /// A void removes the source document's claims and no others — and the
    /// release is real: a claim that no longer exists must not keep occupying
    /// the target's balance.
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

        List<TransactionRatio> remaining = await harness.Db.TransactionRatios.ToListAsync(ct);
        TransactionRatio kept = Assert.Single(remaining);
        Assert.Equal(9702, kept.SourceTransactionId);

        // 300 was released; 400 was still free. 700 is now exactly available.
        Assert.Equal(
            AllocationOutcome.Ok,
            (await harness.Allocations.AllocateAsync(Request("CRN", 9703, "INV", 5009, 700m), ct)).Outcome);
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
        public Task PostInvoiceAsync(long invoiceId, decimal amount, CancellationToken ct) =>
            Postings.PostAsync(new PostLedgerRequest
            {
                TransactionTypeCode = "INV",
                TransactionId = invoiceId,
                LedgerDate = new DateOnly(2026, 8, 1),
                Legs =
                [
                    Leg(Item, 1, RevenueId, credit: amount),
                    Leg(Control, 0, ReceivableId, debit: amount),
                ],
            }, ct);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
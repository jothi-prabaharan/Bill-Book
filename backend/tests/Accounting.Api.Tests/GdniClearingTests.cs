using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Goods Delivered Not Invoiced through the real ledger (TK-90).
///
/// Three writers meet on it: the sale challan posts a provisional <c>Dr GDNI /
/// Cr Inventory</c> per line, the costing worker replaces that with the settled
/// figure on the same key, and the invoice moves the cost on with <c>Dr COGS /
/// Cr GDNI</c> under its own leg type (7), at the cost Inventory holds when it
/// posts — decision D-21 (b). These pin that the three leave Inventory credited
/// once, cost of sales debited once and GDNI at zero, and that a restatement
/// after the invoice leaves a residue rather than an error.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class GdniClearingTests
{
    private const int Control = 3;
    private const int Cogs = 4;
    private const int Gdni = 7;
    private const int DocumentPosting = 1;

    private const long ChallanId = 8101;
    private const long ChallanLine = 81;
    private const long InvoiceId = 8201;
    private const long InvoiceLine = 82;

    private readonly PostgresFixture _postgres;

    public GdniClearingTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public void Every_branch_is_seeded_with_goods_delivered_not_invoiced_off_the_journal_picker()
    {
        Account gdni = Assert.Single(
            ChartOfAccountsSeed.Build(Guid.NewGuid()),
            a => a.AccountSystemName == SystemAccountNames.Of(SystemAccount.GoodsDeliveredNotInvoiced));

        Assert.Equal("Goods Delivered Not Invoiced", gdni.AccountSystemName);
        Assert.Equal(1, gdni.AccountTypeId);
        Assert.False(gdni.IsJE);
        Assert.False(gdni.IsContra);
    }

    [SkippableFact]
    public async Task A_challan_then_its_invoice_leave_gdni_at_zero_and_move_the_cost_once()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        // The challan's provisional cost, then the worker's settled one.
        await h.Postings.PostAsync(h.Challan(450m, provisional: true), ct);
        await h.Postings.PostAsync(h.Challan(462.50m, provisional: false), ct);

        // The invoice clears at what Inventory holds now: the settled figure.
        PostLedgerResult invoice = await h.Postings.PostAsync(h.Invoice(clearing: 462.50m), ct);
        Assert.Equal(PostLedgerOutcome.Ok, invoice.Outcome);

        Assert.Equal(0m, await h.BalanceAsync(h.GdniId));
        Assert.Equal(-462.50m, await h.BalanceAsync(h.InventoryId));
        Assert.Equal(462.50m, await h.BalanceAsync(h.CogsId));
    }

    [SkippableFact]
    public async Task Voiding_the_invoice_puts_the_cost_back_in_gdni()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Postings.PostAsync(h.Challan(450m, provisional: false), ct);
        await h.Postings.PostAsync(h.Invoice(clearing: 450m), ct);

        PostLedgerResult voided = await h.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = InvoiceId,
            LedgerDate = new DateOnly(2026, 8, 2),
            WithdrawLedgerTypeIds = [1, 2, Control, Cogs, 6, Gdni],
            Legs = [],
        }, ct);

        Assert.Equal(PostLedgerOutcome.Ok, voided.Outcome);
        Assert.Equal(450m, await h.BalanceAsync(h.GdniId));
        Assert.Equal(0m, await h.BalanceAsync(h.CogsId));
        Assert.Equal(-450m, await h.BalanceAsync(h.InventoryId));
    }

    /// <summary>
    /// The accepted cost of D-21 (b), not a bug: the invoice cleared at the cost
    /// the challan's movement carried then, and a backdated receipt restates it
    /// afterwards. The worker's replacement lands on the challan's key and posts
    /// cleanly; nothing chases the invoice, so the difference stays in GDNI.
    /// </summary>
    [SkippableFact]
    public async Task A_restatement_after_the_invoice_leaves_a_residue_in_gdni_rather_than_failing()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Postings.PostAsync(h.Challan(450m, provisional: false), ct);
        await h.Postings.PostAsync(h.Invoice(clearing: 450m), ct);

        PostLedgerResult restated = await h.Postings.PostAsync(h.Challan(480m, provisional: false), ct);

        Assert.Equal(PostLedgerOutcome.Ok, restated.Outcome);
        Assert.Equal(30m, await h.BalanceAsync(h.GdniId));
        Assert.Equal(-480m, await h.BalanceAsync(h.InventoryId));
        Assert.Equal(450m, await h.BalanceAsync(h.CogsId));
    }

    /// <summary>
    /// The reason for leg type 7: an invoice line that clears delivered goods
    /// and issues more carries both on one line. The worker's settled posting for
    /// the issue replaces the line's COGS-type rows and must leave the clearing.
    /// </summary>
    [SkippableFact]
    public async Task The_workers_settlement_of_an_issued_line_leaves_its_gdni_clearing_alone()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        PostLedgerRequest invoice = h.Invoice(clearing: 450m);
        invoice.ProvisionalLedgerTypeIds = [Cogs];
        invoice.Legs.Add(Leg(Cogs, InvoiceLine, h.CogsId, debit: 100m));
        invoice.Legs.Add(Leg(Cogs, InvoiceLine, h.InventoryId, credit: 100m));
        invoice.Legs.Single(l => l.AccountId == h.ReceivableId).DebitAmount += 100m;
        invoice.Legs.Single(l => l.AccountId == h.RevenueId).CreditAmount += 100m;
        await h.Postings.PostAsync(invoice, ct);

        // The worker settles the issued part at 104.
        await h.Postings.PostAsync(new PostLedgerRequest
        {
            TransactionTypeCode = "INV",
            TransactionId = InvoiceId,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(Cogs, InvoiceLine, h.CogsId, debit: 104m),
                Leg(Cogs, InvoiceLine, h.InventoryId, credit: 104m),
            ],
        }, ct);

        Assert.Equal(-450m, await h.BalanceAsync(h.GdniId));
        Assert.Equal(554m, await h.BalanceAsync(h.CogsId));
    }

    private static LedgerLegRequest Leg(int type, long detail, long account, decimal debit = 0m, decimal credit = 0m) => new()
    {
        LedgerTypeId = type,
        LedgerSourceId = DocumentPosting,
        TransactionDetailId = detail,
        AccountId = account,
        DebitAmount = debit,
        CreditAmount = credit,
    };

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }

        public required LedgerPostingService Postings { get; init; }

        public required long GdniId { get; init; }

        public required long InventoryId { get; init; }

        public required long CogsId { get; init; }

        public required long ReceivableId { get; init; }

        public required long RevenueId { get; init; }

        /// <summary>What a sale challan's line posts, or the worker's settlement of it: the same key.</summary>
        public PostLedgerRequest Challan(decimal value, bool provisional) => new()
        {
            TransactionTypeCode = "DLC",
            TransactionId = ChallanId,
            LedgerDate = new DateOnly(2026, 8, 1),
            ProvisionalLedgerTypeIds = provisional ? [Cogs] : [],
            Legs =
            [
                Leg(Cogs, ChallanLine, GdniId, debit: value),
                Leg(Cogs, ChallanLine, InventoryId, credit: value),
            ],
        };

        /// <summary>An invoice for 1,000 whose one line clears delivered goods at <paramref name="clearing"/>.</summary>
        public PostLedgerRequest Invoice(decimal clearing) => new()
        {
            TransactionTypeCode = "INV",
            TransactionId = InvoiceId,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                Leg(Control, 0, ReceivableId, debit: 1000m),
                Leg(1, InvoiceLine, RevenueId, credit: 1000m),
                Leg(Gdni, InvoiceLine, CogsId, debit: clearing),
                Leg(Gdni, InvoiceLine, GdniId, credit: clearing),
            ],
        };

        /// <summary>Debits less credits on an account, across every document.</summary>
        public async Task<decimal> BalanceAsync(long accountId) =>
            await Db.JournalLedger.Where(l => l.AccountId == accountId).SumAsync(l => l.DebitAmount - l.CreditAmount);

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            AccountingDbContext db = postgres.CreateContext(Guid.NewGuid(), orgId);

            async Task<long> Account(string code, string name, int typeId)
            {
                var account = new Account
                {
                    OrgId = orgId,
                    AccountTypeId = typeId,
                    AccountCode = code,
                    AccountName = name,
                    AccountSystemName = name,
                    IsActive = true,
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            return new Harness
            {
                Db = db,
                Postings = new LedgerPostingService(
                    db,
                    new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId },
                    new StubBaseCurrency()),
                GdniId = await Account("1250", "Goods Delivered Not Invoiced", 1),
                InventoryId = await Account("1200", "Inventory", 1),
                CogsId = await Account("5100", "Cost of Goods Sold", 5),
                ReceivableId = await Account("1100", "Accounts Receivable", 1),
                RevenueId = await Account("4100", "Sales Revenue", 4),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The till's one call (T7.1, TK-39): a sale made, paid and posted at once, its
/// tenders replacing the receivable, and the last unit sold only once.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PosSaleTests
{
    private const long ItemId = 7001;
    private const long WalkInId = 42;
    private const long CounterCash = 11;
    private const long CardTerminal = 12;

    private readonly PostgresFixture _pg;

    public PosSaleTests(PostgresFixture pg) => _pg = pg;

    // ── Tender rules (no database) ─────────────────────────────────────

    private static PosTenderRequest Tender(PosTenderMode mode, decimal amount, long account = CounterCash) =>
        new() { Mode = mode, Amount = amount, BankAccountId = account };

    [Fact]
    public void Cash_over_the_total_is_change()
    {
        (PosSaleOutcome outcome, decimal change, _) =
            PosSaleService.CheckTenders(118m, [Tender(PosTenderMode.Cash, 200m)]);

        Assert.Equal(PosSaleOutcome.Ok, outcome);
        Assert.Equal(82m, change);
    }

    [Fact]
    public void Tenders_short_of_the_total_are_refused()
    {
        (PosSaleOutcome outcome, _, string? detail) = PosSaleService.CheckTenders(
            118m, [Tender(PosTenderMode.Card, 50m, CardTerminal), Tender(PosTenderMode.Cash, 50m)]);

        Assert.Equal(PosSaleOutcome.TenderShort, outcome);
        Assert.Contains("18.00", detail);
    }

    [Theory]
    [InlineData(PosTenderMode.Card)]
    [InlineData(PosTenderMode.Upi)]
    public void Card_or_upi_over_the_total_is_refused_because_only_cash_gives_change(PosTenderMode mode)
    {
        (PosSaleOutcome outcome, _, _) = PosSaleService.CheckTenders(118m, [Tender(mode, 120m, CardTerminal)]);

        Assert.Equal(PosSaleOutcome.TenderOverpaid, outcome);
    }

    [Fact]
    public void A_split_tender_with_change_from_cash_is_accepted()
    {
        (PosSaleOutcome outcome, decimal change, _) = PosSaleService.CheckTenders(
            118m, [Tender(PosTenderMode.Upi, 100m, CardTerminal), Tender(PosTenderMode.Cash, 20m)]);

        Assert.Equal(PosSaleOutcome.Ok, outcome);
        Assert.Equal(2m, change);
    }

    [Fact]
    public void Change_comes_off_the_cash_tender_and_the_debits_equal_the_total()
    {
        InvoiceTender upi = new() { Mode = PosTenderMode.Upi, Amount = 100m, BankAccountId = CardTerminal };
        InvoiceTender cash = new() { Mode = PosTenderMode.Cash, Amount = 20m, BankAccountId = CounterCash };

        var debits = InvoiceService.TenderDebits(118m, 2m, [upi, cash]);

        Assert.Equal(100m, debits.Single(d => d.Tender == upi).Debit);
        Assert.Equal(18m, debits.Single(d => d.Tender == cash).Debit);
        Assert.Equal(118m, debits.Sum(d => d.Debit));
    }

    [Fact]
    public void Tenders_that_cannot_balance_are_refused_rather_than_posted()
    {
        InvoiceTender card = new() { Mode = PosTenderMode.Card, Amount = 120m, BankAccountId = CardTerminal };

        // Change of 2 with no cash to take it from.
        Assert.Throws<InvalidOperationException>(() => InvoiceService.TenderDebits(118m, 2m, [card]));
    }

    // ── Against a database ─────────────────────────────────────────────

    /// <summary>One unit on hand, shared by every till: the guarded decrement in miniature.</summary>
    private sealed class LastUnitInventory : IInventoryClient
    {
        private int _onHand = 1;

        public Task<IssueStockResponse> IssueAsync(IssueStockRequest request, CancellationToken ct)
        {
            bool taken = Interlocked.CompareExchange(ref _onHand, 0, 1) == 1;

            return Task.FromResult(new IssueStockResponse
            {
                Success = taken,
                TotalValue = taken ? 50m : 0m,
                Lines = request.Lines.Select(l => new IssueStockLineResult
                {
                    SourceLineId = l.SourceLineId,
                    ItemId = l.ItemId,
                    RequestedQuantity = l.Quantity,
                    Success = taken,
                    Outcome = taken ? "Ok" : "InsufficientStock",
                    StockMovementId = taken ? 9001 : null,
                    UnitCost = taken ? 50m : 0m,
                    LineValue = taken ? 50m : 0m,
                }).ToList(),
            });
        }

        public Task<ReserveStockResponse> ReserveAsync(ReserveStockRequest request, CancellationToken ct) =>
            Task.FromResult(new ReserveStockResponse { Success = true });

        public Task<ReleaseStockResponse> ReleaseAsync(ReleaseStockRequest request, CancellationToken ct) =>
            Task.FromResult(new ReleaseStockResponse { Success = true });

        public Task<ReceiveStockResponse> ReceiveAsync(ReceiveStockRequest request, CancellationToken ct) =>
            Task.FromResult(new ReceiveStockResponse { Success = true });

        public Task<StockAvailabilityResponse> GetAvailabilityAsync(StockAvailabilityRequest request, CancellationToken ct) =>
            Task.FromResult(new StockAvailabilityResponse());

        public Task<StockMovementCostsResponse?> GetMovementCostsAsync(StockMovementCostsRequest request, CancellationToken ct) =>
            Task.FromResult<StockMovementCostsResponse?>(new StockMovementCostsResponse());
    }

    private sealed record Till(SalesDbContext Db, PosSaleService Sales, RecordingLedger Ledger, InvoiceService Invoices);

    private static Till OpenTill(PostgresFixture pg, Guid customerId, Guid orgId, IInventoryClient inventory)
    {
        TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" };
        SalesDbContext db = pg.CreateContext(customerId, orgId);
        StubNameLookup names = new();
        RecordingLedger ledger = new();
        StubCurrentUser cashier = new();

        InvoiceService invoices = new(
            db, tenant,
            new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
            new StubBaseCurrency(), new StubBranchSettings(), new StubTaxRates(),
            names, names, cashier, TimeProvider.System, inventory, ledger,
            new StubCreditCheck(), new StubDocumentStorage(), new StubInvoicePdf(), new StubOrgIdentity(), new StubUqcLookup(), new StubEInvoicing());

        return new Till(db, new PosSaleService(invoices, db, cashier, TimeProvider.System), ledger, invoices);
    }

    private static async Task<(Guid CustomerId, Guid OrgId)> NewBranchAsync(PostgresFixture pg)
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using SalesDbContext db = pg.CreateContext(customerId, orgId);
        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
        await db.SaveChangesAsync();
        return (customerId, orgId);
    }

    private static PosSaleRequest Sale(params PosTenderRequest[] tenders) => new()
    {
        TillId = 1,
        DocumentDate = new DateOnly(2026, 9, 24),
        ContactId = WalkInId,
        PlaceOfSupplyStateCode = "33",
        Lines =
        [
            new SaveInvoiceLineRequest
            {
                ItemId = ItemId,
                Description = "Soap",
                Quantity = 1m,
                ConversionFactor = 1m,
                UnitPrice = 100m,
                TaxGroupId = 1,
                LineType = DocumentLineType.Stock,
            },
        ],
        Tenders = [.. tenders],
    };

    [SkippableFact]
    public async Task A_cash_sale_posts_to_the_drawer_not_the_receivable_and_records_its_change()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Till till = OpenTill(_pg, customerId, orgId, new RecordingInventory());
        await using SalesDbContext _ = till.Db;

        // 100 + 18% = 118.
        PosSaleResult result = await till.Sales.SellAsync(Sale(Tender(PosTenderMode.Cash, 200m)), default);

        Assert.Equal(PosSaleOutcome.Ok, result.Outcome);
        Assert.Equal(118m, result.TotalAmount);
        Assert.Equal(82m, result.ChangeAmount);
        Assert.StartsWith("POS", result.DocumentNo);

        PostLedgerRequest post = Assert.Single(till.Ledger.Posts);
        Assert.Equal("POS", post.TransactionTypeCode);
        LedgerLegRequest drawer = Assert.Single(post.Legs, l => l.BankAccountId == CounterCash);
        Assert.Equal(118m, drawer.DebitAmount);
        Assert.DoesNotContain(post.Legs, l => l.AccountSystemName == "Accounts Receivable");

        Invoice invoice = await till.Db.Invoices.AsNoTracking().SingleAsync(i => i.InvoiceId == result.InvoiceId);
        Assert.Equal(DocumentStatus.Posted, invoice.Status);
        Assert.Equal("Cash", invoice.PaymentMode);
        Assert.Equal(200m, invoice.TenderedAmount);
        Assert.Equal(82m, invoice.ChangeAmount);
        Assert.Single(await till.Db.InvoiceTenders.AsNoTracking().Where(t => t.InvoiceId == result.InvoiceId).ToListAsync());
    }

    /// <summary>The receipt (TK-41) and a reprint read the tenders off the invoice view.</summary>
    [SkippableFact]
    public async Task The_invoice_view_carries_the_tenders_a_receipt_prints()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Till till = OpenTill(_pg, customerId, orgId, new RecordingInventory());
        await using SalesDbContext _ = till.Db;

        PosSaleResult result = await till.Sales.SellAsync(
            Sale(Tender(PosTenderMode.Card, 18m, CardTerminal), Tender(PosTenderMode.Cash, 200m)), default);
        Assert.Equal(PosSaleOutcome.Ok, result.Outcome);

        InvoiceView? view = await till.Invoices.GetAsync(result.InvoiceId, default);

        Assert.NotNull(view);
        Assert.Equal(["Card", "Cash"], view.Tenders.Select(t => t.Mode));
        Assert.Equal(218m, view.Tenders.Sum(t => t.Amount));
        Assert.Equal(100m, view.ChangeAmount);
    }

    [SkippableFact]
    public async Task Tenders_that_do_not_cover_the_total_post_nothing()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Till till = OpenTill(_pg, customerId, orgId, new RecordingInventory());
        await using SalesDbContext _ = till.Db;

        PosSaleResult result = await till.Sales.SellAsync(Sale(Tender(PosTenderMode.Cash, 100m)), default);

        Assert.Equal(PosSaleOutcome.TenderShort, result.Outcome);
        Assert.Empty(till.Ledger.Posts);
        Assert.Empty(await till.Db.InvoiceTenders.AsNoTracking().ToListAsync());
    }

    /// <summary>
    /// The card's Done-when line: two tills sell the last unit at the same
    /// moment, and exactly one sale stands. The loser is told which item ran out.
    /// </summary>
    [SkippableFact]
    public async Task Two_concurrent_sales_of_the_last_unit_leave_exactly_one_sale()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        var inventory = new LastUnitInventory();
        Till first = OpenTill(_pg, customerId, orgId, inventory);
        Till second = OpenTill(_pg, customerId, orgId, inventory);
        await using SalesDbContext _1 = first.Db;
        await using SalesDbContext _2 = second.Db;

        PosSaleResult[] results = await Task.WhenAll(
            first.Sales.SellAsync(Sale(Tender(PosTenderMode.Cash, 118m)), default),
            second.Sales.SellAsync(Sale(Tender(PosTenderMode.Cash, 118m)), default));

        Assert.Single(results, r => r.Outcome == PosSaleOutcome.Ok);
        PosSaleResult lost = Assert.Single(results, r => r.Outcome == PosSaleOutcome.InvoiceRefused);
        Assert.Equal(InvoiceOutcome.InsufficientStock, lost.Refusal);
        Assert.Contains($"Name {ItemId}", lost.Detail);

        await using SalesDbContext check = _pg.CreateContext(customerId, orgId);
        Assert.Equal(1, await check.Invoices.CountAsync(i => i.TransactionTypeCode == "POS" && i.Status == DocumentStatus.Posted));
        Assert.Single(first.Ledger.Posts.Concat(second.Ledger.Posts));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Repository.Repositories;
using Sales.Repository.Clients;
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
            PosSaleRepository.CheckTenders(118m, [Tender(PosTenderMode.Cash, 200m)]);

        Assert.Equal(PosSaleOutcome.Ok, outcome);
        Assert.Equal(82m, change);
    }

    [Fact]
    public void Tenders_short_of_the_total_are_refused()
    {
        (PosSaleOutcome outcome, _, string? detail) = PosSaleRepository.CheckTenders(
            118m, [Tender(PosTenderMode.Card, 50m, CardTerminal), Tender(PosTenderMode.Cash, 50m)]);

        Assert.Equal(PosSaleOutcome.TenderShort, outcome);
        Assert.Contains("18.00", detail);
    }

    [Theory]
    [InlineData(PosTenderMode.Card)]
    [InlineData(PosTenderMode.Upi)]
    public void Card_or_upi_over_the_total_is_refused_because_only_cash_gives_change(PosTenderMode mode)
    {
        (PosSaleOutcome outcome, _, _) = PosSaleRepository.CheckTenders(118m, [Tender(mode, 120m, CardTerminal)]);

        Assert.Equal(PosSaleOutcome.TenderOverpaid, outcome);
    }

    [Fact]
    public void A_split_tender_with_change_from_cash_is_accepted()
    {
        (PosSaleOutcome outcome, decimal change, _) = PosSaleRepository.CheckTenders(
            118m, [Tender(PosTenderMode.Upi, 100m, CardTerminal), Tender(PosTenderMode.Cash, 20m)]);

        Assert.Equal(PosSaleOutcome.Ok, outcome);
        Assert.Equal(2m, change);
    }

    [Fact]
    public void Change_comes_off_the_cash_tender_and_the_debits_equal_the_total()
    {
        InvoiceTender upi = new() { Mode = PosTenderMode.Upi, Amount = 100m, BankAccountId = CardTerminal };
        InvoiceTender cash = new() { Mode = PosTenderMode.Cash, Amount = 20m, BankAccountId = CounterCash };

        var debits = InvoiceRepository.TenderDebits(118m, 2m, [upi, cash]);

        Assert.Equal(100m, debits.Single(d => d.Tender == upi).Debit);
        Assert.Equal(18m, debits.Single(d => d.Tender == cash).Debit);
        Assert.Equal(118m, debits.Sum(d => d.Debit));
    }

    [Fact]
    public void Tenders_that_cannot_balance_are_refused_rather_than_posted()
    {
        InvoiceTender card = new() { Mode = PosTenderMode.Card, Amount = 120m, BankAccountId = CardTerminal };

        // Change of 2 with no cash to take it from.
        Assert.Throws<InvalidOperationException>(() => InvoiceRepository.TenderDebits(118m, 2m, [card]));
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

    private sealed record Till(SalesDbContext Db, PosSaleRepository Sales, RecordingLedger Ledger, InvoiceRepository Invoices);

    private static Till OpenTill(
        PostgresFixture pg, Guid customerId, Guid orgId, IInventoryClient inventory,
        IBranchSettingsProvider? branch = null)
    {
        TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" };
        SalesDbContext db = pg.CreateContext(customerId, orgId);
        StubNameLookup names = new();
        RecordingLedger ledger = new();
        StubCurrentUser cashier = new();

        // One branch for the invoice and for the till on top of it, as in the service.
        // Tamil Nadu (33) unless a test says otherwise.
        IBranchSettingsProvider settings = branch ?? new StubBranchSettings();

        InvoiceRepository invoices = new(
            db, tenant,
            new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
            new StubBaseCurrency(), settings, new StubTaxRates(),
            names, names, cashier, TimeProvider.System, inventory, ledger,
            new StubCreditCheck(), new StubUqcLookup(), new StubEInvoicing());

        return new Till(db, new PosSaleRepository(invoices, db, settings, cashier, TimeProvider.System), ledger, invoices);
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

    /// <summary>
    /// A sale as the till sends it to the walk-in customer: no GSTIN and no place
    /// of supply (D-73). It once stated a place of supply of its own, which is why
    /// no test noticed that the till's real sale was refused.
    /// </summary>
    private static PosSaleRequest Sale(params PosTenderRequest[] tenders) => new()
    {
        TillId = 1,
        DocumentDate = new DateOnly(2026, 9, 24),
        ContactId = WalkInId,
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

    // ── Place of supply (D-73, TK-270) ─────────────────────────────────
    //
    // Sale() sends what the till sends: the walk-in customer, no GSTIN and no
    // place of supply. A test sets either one only when it is about that.

    /// <summary>A till sale as stored, with the tax rows of every line, read back from the database.</summary>
    private static async Task<(Invoice Invoice, List<InvoiceDetailTax> Taxes)> PostedAsync(
        SalesDbContext db, long invoiceId)
    {
        Invoice invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Lines).ThenInclude(l => l.Taxes)
            .SingleAsync(i => i.InvoiceId == invoiceId);

        return (invoice, [.. invoice.Lines.SelectMany(l => l.Taxes)]);
    }

    /// <summary>Branch settings that cannot be read: the transient-failure path.</summary>
    private sealed class UnreadableBranchSettings : IBranchSettingsProvider
    {
        public Task<BranchSettings?> GetSettingsAsync(CancellationToken ct = default) =>
            Task.FromResult<BranchSettings?>(null);
    }

    /// <summary>
    /// The card's Done-when line. The walk-in customer is a Consumer and cannot hold a
    /// GSTIN, and the till states no place of supply, so this sale was refused with
    /// "Set a place of supply, or a GSTIN to take it from" until D-73.
    /// </summary>
    [SkippableFact]
    public async Task A_sale_to_a_customer_with_no_gstin_takes_the_branchs_own_state()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Till till = OpenTill(_pg, customerId, orgId, new RecordingInventory());
        await using SalesDbContext _ = till.Db;

        PosSaleRequest sale = Sale(Tender(PosTenderMode.Cash, 118m));
        Assert.Null(sale.ContactGstin);
        Assert.Null(sale.PlaceOfSupplyStateCode);

        PosSaleResult result = await till.Sales.SellAsync(sale, default);

        Assert.Equal(PosSaleOutcome.Ok, result.Outcome);
        Assert.Equal(118m, result.TotalAmount);

        (Invoice invoice, List<InvoiceDetailTax> taxes) = await PostedAsync(till.Db, result.InvoiceId);

        Assert.Equal(DocumentStatus.Posted, invoice.Status);
        Assert.False(invoice.IsInterState);
        Assert.Null(invoice.ContactGstin);

        // 100 at 18% is nine and nine, and no IGST.
        Assert.Equal(2, taxes.Count);
        Assert.Contains(taxes, t => t.TaxComponent == TaxComponent.Cgst && t.Amount == 9m);
        Assert.Contains(taxes, t => t.TaxComponent == TaxComponent.Sgst && t.Amount == 9m);
    }

    /// <summary>
    /// The state is read from the branch, not fixed: every other test's branch is
    /// in Tamil Nadu (33), so a default that was hard-coded there would pass them.
    /// </summary>
    [SkippableFact]
    public async Task The_state_is_the_branchs_own_and_not_a_fixed_one()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Till till = OpenTill(_pg, customerId, orgId, new RecordingInventory(), new StubBranchSettings("29"));
        await using SalesDbContext _ = till.Db;

        PosSaleResult result = await till.Sales.SellAsync(Sale(Tender(PosTenderMode.Cash, 118m)), default);

        Assert.Equal(PosSaleOutcome.Ok, result.Outcome);

        (Invoice invoice, List<InvoiceDetailTax> taxes) = await PostedAsync(till.Db, result.InvoiceId);

        Assert.False(invoice.IsInterState);
        Assert.Equal(2, taxes.Count);
        Assert.DoesNotContain(taxes, t => t.TaxComponent == TaxComponent.Igst);
    }

    [SkippableFact]
    public async Task A_customer_gstin_in_another_state_still_makes_it_inter_state()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Till till = OpenTill(_pg, customerId, orgId, new RecordingInventory());
        await using SalesDbContext _ = till.Db;

        // The branch is in Tamil Nadu (33); this customer is registered in Karnataka (29).
        PosSaleRequest sale = Sale(Tender(PosTenderMode.Cash, 118m));
        sale.ContactGstin = "29AAAAA0000A1Z5";

        PosSaleResult result = await till.Sales.SellAsync(sale, default);

        Assert.Equal(PosSaleOutcome.Ok, result.Outcome);

        (Invoice invoice, List<InvoiceDetailTax> taxes) = await PostedAsync(till.Db, result.InvoiceId);

        Assert.True(invoice.IsInterState);
        InvoiceDetailTax igst = Assert.Single(taxes);
        Assert.Equal(TaxComponent.Igst, igst.TaxComponent);
        Assert.Equal(18m, igst.Amount);
    }

    [SkippableFact]
    public async Task A_stated_place_of_supply_is_kept_and_not_replaced_by_the_branchs()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Till till = OpenTill(_pg, customerId, orgId, new RecordingInventory());
        await using SalesDbContext _ = till.Db;

        // A customer with no GSTIN, but a place of supply that is not the branch's.
        PosSaleRequest sale = Sale(Tender(PosTenderMode.Cash, 118m));
        sale.PlaceOfSupplyStateCode = "29";

        PosSaleResult result = await till.Sales.SellAsync(sale, default);

        Assert.Equal(PosSaleOutcome.Ok, result.Outcome);

        (Invoice invoice, List<InvoiceDetailTax> taxes) = await PostedAsync(till.Db, result.InvoiceId);

        Assert.True(invoice.IsInterState);
        Assert.Equal(TaxComponent.Igst, Assert.Single(taxes).TaxComponent);
    }

    /// <summary>The default fills a gap and settles no disagreement: the two stay refused.</summary>
    [SkippableFact]
    public async Task A_gstin_that_contradicts_a_stated_place_of_supply_is_still_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Till till = OpenTill(_pg, customerId, orgId, new RecordingInventory());
        await using SalesDbContext _ = till.Db;

        PosSaleRequest sale = Sale(Tender(PosTenderMode.Cash, 118m));
        sale.ContactGstin = "29AAAAA0000A1Z5";
        sale.PlaceOfSupplyStateCode = "33";

        PosSaleResult result = await till.Sales.SellAsync(sale, default);

        Assert.Equal(PosSaleOutcome.InvoiceRefused, result.Outcome);
        Assert.Equal(InvoiceOutcome.PlaceOfSupplyRefused, result.Refusal);
        Assert.Contains("registered in state 29", result.Detail);
        Assert.Empty(till.Ledger.Posts);
    }

    /// <summary>
    /// The branch's state is needed and cannot be read: the sale is refused as
    /// unavailable, which is a 503, and not as a missing place of supply, which
    /// would send the cashier looking for a field the till does not have.
    /// </summary>
    [SkippableFact]
    public async Task Branch_settings_that_cannot_be_read_refuse_the_sale_as_unavailable()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Till till = OpenTill(_pg, customerId, orgId, new RecordingInventory(), new UnreadableBranchSettings());
        await using SalesDbContext _ = till.Db;

        PosSaleResult result = await till.Sales.SellAsync(Sale(Tender(PosTenderMode.Cash, 118m)), default);

        Assert.Equal(PosSaleOutcome.InvoiceRefused, result.Outcome);
        Assert.Equal(InvoiceOutcome.RatesUnavailable, result.Refusal);
        Assert.Contains("Branch settings", result.Detail);
        Assert.Empty(till.Ledger.Posts);
    }

    /// <summary>
    /// The default belongs to the till alone: any other invoice, with no GSTIN
    /// and no place of supply, must still say where its supply is made.
    /// </summary>
    [SkippableFact]
    public async Task An_ordinary_invoice_with_no_gstin_and_no_place_of_supply_is_still_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customerId, Guid orgId) = await NewBranchAsync(_pg);
        Till till = OpenTill(_pg, customerId, orgId, new RecordingInventory());
        await using SalesDbContext _ = till.Db;

        InvoiceResult result = await till.Invoices.CreateAsync(new SaveInvoiceRequest
        {
            DocumentDate = new DateOnly(2026, 9, 24),
            DueDate = new DateOnly(2026, 10, 24),
            ContactId = WalkInId,
            Lines = Sale(Tender(PosTenderMode.Cash, 118m)).Lines,
        }, default);

        Assert.Equal(InvoiceOutcome.PlaceOfSupplyRefused, result.Outcome);
        Assert.Contains("Set a place of supply", result.Detail);
    }
}

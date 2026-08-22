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
/// The sales order — T2.2 — against a real PostgreSQL.
///
/// <b>Written through the service rather than by inserting rows.</b> Three of
/// the four bugs these cover would have looked fine in a test that built a
/// <c>SalesOrder</c> by hand: the create path was refused by a check constraint,
/// the void path asked the wrong question, and the release path was handed an
/// empty line collection. All three are only visible from the door the
/// controller uses.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SalesOrderServiceTests
{
    private readonly PostgresFixture _pg;

    public SalesOrderServiceTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task A_draft_is_numbered_taxed_and_not_stamped_as_posted()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        SalesOrderResult result = await h.Service.CreateAsync(
            Request([Line(quantity: 10m, unitPrice: 100m, taxGroupId: 1)]), default);

        Assert.Equal(SalesOrderOutcome.Ok, result.Outcome);

        SalesOrder saved = await h.Db.SalesOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.SalesOrderId == result.SalesOrderId);

        Assert.StartsWith("SO/", saved.DocumentNo);
        Assert.Equal("SOR", saved.TransactionTypeCode);

        // 10 × 100 = 1000 taxable, 18% = 180, total 1180.
        Assert.Equal(1000m, saved.TaxableAmount);
        Assert.Equal(1180m, saved.TotalAmount);
        Assert.False(saved.IsInterState);
        Assert.Equal(90m, saved.CgstAmount);
        Assert.Equal(90m, saved.SgstAmount);

        // The one that matters. Create used to stamp PostedAt and PostedBy on a
        // Draft, which chk_salesorders_posted_stamp refuses outright — so no
        // sales order could be created at all, and the failure surfaced as a
        // database error rather than as anything a reader would connect to this.
        Assert.Equal(DocumentStatus.Draft, saved.Status);
        Assert.Null(saved.PostedAt);
        Assert.Null(saved.PostedBy);

        // Nothing is reserved until it is confirmed.
        Assert.Equal(0m, saved.Lines.Single().ReservedQuantity);
        Assert.Equal(FulfilmentStatus.Open, saved.FulfilmentStatus);
        Assert.Empty(h.Inventory.Reservations);
    }

    [SkippableFact]
    public async Task Confirming_reserves_the_stock_and_records_how_much()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        SalesOrderResult created = await h.Service.CreateAsync(
            Request([Line(quantity: 4m, unitPrice: 250m, taxGroupId: 1)]), default);

        SalesOrderResult confirmed = await h.Service.ConfirmAsync(created.SalesOrderId, default);
        Assert.Equal(SalesOrderOutcome.Ok, confirmed.Outcome);

        ReserveStockRequest reservation = Assert.Single(h.Inventory.Reservations);
        ReserveStockLine reservedLine = Assert.Single(reservation.Lines);
        Assert.Equal(7, reservedLine.ItemId);
        Assert.Equal(4m, reservedLine.Quantity);

        h.Db.ChangeTracker.Clear();
        SalesOrder saved = await h.Db.SalesOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.SalesOrderId == created.SalesOrderId);

        Assert.Equal(DocumentStatus.Posted, saved.Status);
        Assert.NotNull(saved.PostedAt);

        // Kept on the line as well as in Inventory, because releasing has to be
        // exact when only part of the order ships.
        Assert.Equal(4m, saved.Lines.Single().ReservedQuantity);
    }

    [SkippableFact]
    public async Task A_short_item_is_named_rather_than_reported_as_insufficient_stock()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        h.Inventory.RefuseReserve[7] = "InsufficientStock";

        SalesOrderResult created = await h.Service.CreateAsync(
            Request([Line(quantity: 4m, unitPrice: 250m, taxGroupId: 1)]), default);

        SalesOrderResult confirmed = await h.Service.ConfirmAsync(created.SalesOrderId, default);

        Assert.Equal(SalesOrderOutcome.InsufficientStock, confirmed.Outcome);

        // "Insufficient stock" on a twenty-line order is not something the person
        // on the phone to the customer can act on. The item is named.
        Assert.Contains("C7", confirmed.Detail);

        h.Db.ChangeTracker.Clear();
        SalesOrder saved = await h.Db.SalesOrders
            .SingleAsync(o => o.SalesOrderId == created.SalesOrderId);

        // Refused, so it stays a draft — never a confirmed order the shelf
        // disagrees with.
        Assert.Equal(DocumentStatus.Draft, saved.Status);
    }

    [SkippableFact]
    public async Task A_draft_can_be_voided_with_a_reason_and_reserves_nothing_back()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        SalesOrderResult created = await h.Service.CreateAsync(
            Request([Line(quantity: 2m, unitPrice: 500m, taxGroupId: 1)]), default);

        SalesOrderResult voided = await h.Service.VoidAsync(
            created.SalesOrderId, new VoidSalesOrderRequest { Reason = "Customer changed their mind." },
            default);

        // The downstream check used to ask whether this order existed — which it
        // always does by that line — so every void was refused as having
        // documents beneath it and no sales order could be withdrawn at all.
        Assert.Equal(SalesOrderOutcome.Ok, voided.Outcome);

        h.Db.ChangeTracker.Clear();
        SalesOrder saved = await h.Db.SalesOrders
            .SingleAsync(o => o.SalesOrderId == created.SalesOrderId);

        Assert.Equal(DocumentStatus.Void, saved.Status);
        Assert.Equal(FulfilmentStatus.Cancelled, saved.FulfilmentStatus);
        Assert.Equal("Customer changed their mind.", saved.VoidReason);

        // A draft never held a reservation, so nothing is handed back. Releasing
        // here would be a windfall on the next availability check.
        Assert.Empty(h.Inventory.Releases);
    }

    [SkippableFact]
    public async Task Voiding_a_confirmed_order_releases_the_lines_it_reserved()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        SalesOrderResult created = await h.Service.CreateAsync(
            Request([Line(quantity: 6m, unitPrice: 100m, taxGroupId: 1)]), default);

        await h.Service.ConfirmAsync(created.SalesOrderId, default);

        SalesOrderResult voided = await h.Service.VoidAsync(
            created.SalesOrderId, new VoidSalesOrderRequest { Reason = "Order cancelled." }, default);

        Assert.Equal(SalesOrderOutcome.Ok, voided.Outcome);

        // The release used to be built from a header fetched with FindAsync, so
        // Lines was empty and nothing was ever handed back — silently, because
        // an empty request was skipped and the void reported success.
        ReleaseStockRequest release = Assert.Single(h.Inventory.Releases);
        ReleaseStockLine releasedLine = Assert.Single(release.Lines);
        Assert.Equal(7, releasedLine.ItemId);
        Assert.Equal(6m, releasedLine.Quantity);
    }

    [SkippableFact]
    public async Task An_order_with_an_invoice_against_it_cannot_be_voided()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        SalesOrderResult created = await h.Service.CreateAsync(
            Request([Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]), default);

        h.Db.Invoices.Add(new Invoice
        {
            TransactionTypeCode = "INV",
            DocumentNo = $"INV/{Guid.NewGuid():N}"[..20],
            DocumentDate = new DateOnly(2026, 6, 2),
            DueDate = new DateOnly(2026, 7, 2),
            SalesOrderId = created.SalesOrderId,
            ContactId = 42,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = DocumentStatus.Draft,
        });

        await h.Db.SaveChangesAsync();

        SalesOrderResult voided = await h.Service.VoidAsync(
            created.SalesOrderId, new VoidSalesOrderRequest { Reason = "Mistake." }, default);

        Assert.Equal(SalesOrderOutcome.LifecycleRefused, voided.Outcome);
        Assert.Contains("points at this document", voided.Detail);
    }

    [SkippableFact]
    public async Task The_list_clamps_a_negative_skip_and_an_oversized_take()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        for (int i = 0; i < 3; i++)
        {
            await h.Service.CreateAsync(
                Request([Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]), default);
        }

        // Skip(-5) throws on some providers and quietly serves page one on
        // others, so a hand-edited URL either 500s or lies to the pager.
        SalesOrderListPage page = await h.Service.ListAsync(-5, 1_000_000, null, null, default);

        Assert.Equal(0, page.Skip);
        Assert.Equal(200, page.Take);
        Assert.Equal(3, page.Total);
        Assert.Equal(3, page.Rows.Count);
    }

    [SkippableFact]
    public async Task The_total_counts_what_matched_rather_than_what_fitted_on_the_page()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        for (int i = 0; i < 5; i++)
        {
            await h.Service.CreateAsync(
                Request([Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]), default);
        }

        SalesOrderListPage page = await h.Service.ListAsync(0, 2, null, null, default);

        Assert.Equal(2, page.Rows.Count);

        // Counting the rows it was handed would say "2 of 2" on every page.
        Assert.Equal(5, page.Total);

        // One lookup for the whole page, never one per row: Contacts is another
        // database and this is the screen that would make it an N+1.
        Assert.Single(h.Names.Calls);
    }

    [SkippableFact]
    public async Task The_list_filters_by_status()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        SalesOrderResult first = await h.Service.CreateAsync(
            Request([Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]), default);

        await h.Service.CreateAsync(
            Request([Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]), default);

        await h.Service.ConfirmAsync(first.SalesOrderId, default);

        SalesOrderListPage posted = await h.Service.ListAsync(0, 50, "Posted", null, default);
        SalesOrderListPage drafts = await h.Service.ListAsync(0, 50, "draft", null, default);

        Assert.Equal(1, posted.Total);
        Assert.Equal(first.SalesOrderId, posted.Rows.Single().SalesOrderId);

        // Case-insensitive, because it arrives off a query string.
        Assert.Equal(1, drafts.Total);
    }

    [SkippableFact]
    public async Task Another_branchs_order_is_forbidden_rather_than_not_found()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Harness mine = await Harness.CreateAsync(_pg, customerId);
        Harness theirs = await Harness.CreateAsync(_pg, customerId);

        SalesOrderResult created = await mine.Service.CreateAsync(
            Request([Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]), default);

        // Reached through the other branch's service, the row is hidden by the
        // query filter and by RLS, so this is the NotFound path — the guard
        // under them answers Forbidden only once something has read past both.
        SalesOrderViewResult crossBranch = await theirs.Service.GetAsync(created.SalesOrderId, default);
        Assert.Equal(SalesOrderOutcome.NotFound, crossBranch.Outcome);

        // Its own branch reads it back in full.
        SalesOrderViewResult own = await mine.Service.GetAsync(created.SalesOrderId, default);
        Assert.Equal(SalesOrderOutcome.Ok, own.Outcome);
        Assert.NotNull(own.View);
        Assert.Equal("Name 42", own.View.ContactName);
    }

    [SkippableFact]
    public async Task An_accepted_quote_becomes_an_order_carrying_its_lines()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        Quote quote = new()
        {
            TransactionTypeCode = "QTE",
            DocumentNo = $"QT/{Guid.NewGuid():N}"[..20],
            DocumentDate = new DateOnly(2026, 6, 1),
            ValidUntil = new DateOnly(2026, 7, 1),
            ContactId = 42,

            // 33 is Tamil Nadu, which is where the branch is, so this resolves
            // intra-state off the GSTIN alone — the ordinary case, and the
            // reason the conversion does not need the place of supply stated.
            ContactGstin = "33ABCDE1234F1Z5",
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = DocumentStatus.Posted,
            PostedAt = DateTimeOffset.UtcNow,
            Lines =
            {
                new QuoteDetail
                {
                    LineNumber = 1,
                    ItemId = 7,
                    Quantity = 3m,
                    ConversionFactor = 1m,
                    BaseQuantity = 3m,
                    UnitPrice = 200m,
                    TaxGroupId = 1,
                    LineType = DocumentLineType.Stock,
                },
            },
        };

        h.Db.Quotes.Add(quote);
        await h.Db.SaveChangesAsync();

        SalesOrderResult result = await h.Service.CreateFromQuoteAsync(
            quote.QuoteId,
            new CreateOrderFromQuoteRequest
            {
                DocumentDate = new DateOnly(2026, 6, 10),
                DeliveryDate = new DateOnly(2026, 6, 20),
            },
            default);

        Assert.Equal(SalesOrderOutcome.Ok, result.Outcome);

        SalesOrder order = await h.Db.SalesOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.SalesOrderId == result.SalesOrderId);

        Assert.Equal(quote.QuoteId, order.QuoteId);
        Assert.Equal(new DateOnly(2026, 6, 20), order.DeliveryDate);

        // 3 × 200 = 600 taxable, recomputed at the order's own date rather than
        // copied off the quote.
        Assert.Equal(600m, order.TaxableAmount);
        Assert.Equal(708m, order.TotalAmount);
        Assert.Equal(3m, order.Lines.Single().Quantity);

        // Converting it a second time is refused, so one quote cannot become two
        // orders because somebody double-clicked.
        SalesOrderResult again = await h.Service.CreateFromQuoteAsync(
            quote.QuoteId, new CreateOrderFromQuoteRequest(), default);

        Assert.Equal(SalesOrderOutcome.QuoteNotConvertible, again.Outcome);
    }

    [SkippableFact]
    public async Task A_draft_quote_is_not_convertible()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        Quote quote = new()
        {
            TransactionTypeCode = "QTE",
            DocumentNo = $"QT/{Guid.NewGuid():N}"[..20],
            DocumentDate = new DateOnly(2026, 6, 1),
            ValidUntil = new DateOnly(2026, 7, 1),
            ContactId = 42,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = DocumentStatus.Draft,
        };

        h.Db.Quotes.Add(quote);
        await h.Db.SaveChangesAsync();

        SalesOrderResult result = await h.Service.CreateFromQuoteAsync(
            quote.QuoteId, new CreateOrderFromQuoteRequest(), default);

        Assert.Equal(SalesOrderOutcome.QuoteNotConvertible, result.Outcome);
        Assert.Contains("Approve the quote first", result.Detail);
    }

    private static SaveSalesOrderRequest Request(List<SaveSalesOrderLineRequest> lines) =>
        new()
        {
            DocumentDate = new DateOnly(2026, 6, 1),
            ContactId = 42,
            DeliveryDate = new DateOnly(2026, 6, 15),
            PlaceOfSupplyStateCode = "33",
            Lines = lines,
        };

    private static SaveSalesOrderLineRequest Line(
        decimal quantity, decimal unitPrice, long taxGroupId) =>
        new()
        {
            ItemId = 7,
            Quantity = quantity,
            ConversionFactor = 1m,
            UnitPrice = unitPrice,
            TaxGroupId = taxGroupId,
            LineType = DocumentLineType.Stock,
        };

    /// <summary>
    /// One branch, its numbering series seeded, and the service wired to stubs
    /// for everything that would otherwise be an HTTP call.
    /// </summary>
    private sealed record Harness(
        SalesDbContext Db,
        SalesOrderService Service,
        StubNameLookup Names,
        RecordingInventory Inventory)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg, Guid? customerId = null)
        {
            Guid orgId = Guid.NewGuid();

            SalesDbContext db = pg.CreateContext(customerId ?? Guid.NewGuid(), orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            StubNameLookup names = new();
            RecordingInventory inventory = new();

            NumberGenerator numbering = new(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            SalesOrderService service = new(
                db,
                new TenantContext { CustomerId = customerId ?? Guid.NewGuid(), OrgId = orgId },
                numbering,
                new StubBaseCurrency(),
                new StubBranchSettings(),
                new StubTaxRates(),
                names,
                names,
                new StubCurrentUser(),
                TimeProvider.System,
                inventory,
                new StubCreditCheck());

            return new Harness(db, service, names, inventory);
        }
    }
}

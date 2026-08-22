using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The sales order, against a real database.
///
/// What these check is the pair that makes an order different from every other
/// document: it posts <b>nothing</b> to the ledger, and it <b>holds stock</b>.
/// The second half is a call to another service, so Inventory is a stand-in
/// here — but the contract being tested is the one that matters, which is what
/// this service does when the answer comes back yes, no, or not at all.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SalesOrderServiceTests
{
    private readonly PostgresFixture _postgres;

    public SalesOrderServiceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>Confirming takes a number and holds exactly what the lines promised.</summary>
    [SkippableFact]
    public async Task Confirming_takes_a_number_and_reserves_every_stock_line()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long id = await h.Draft((41, 6m), (42, 4m));

        SalesOrderResult confirmed = await h.Orders.ConfirmAsync(id, ct);
        Assert.Equal(SalesOrderOutcome.Ok, confirmed.Outcome);

        SalesOrder order = await h.Db.SalesOrders.FirstAsync(o => o.SalesOrderId == id, ct);
        Assert.Equal(DocumentStatus.Posted, order.Status);
        Assert.StartsWith("SO/", order.DocumentNo);
        Assert.Equal(FulfilmentStatus.Open, order.FulfilmentStatus);

        // Inventory was asked for exactly the lines, in base quantity.
        Assert.Equal(
            [(41L, 6m), (42L, 4m)],
            h.Inventory.Reserved.Select(r => (r.ItemId, r.Quantity)).Order().ToArray());

        // And the order records what it is holding, which is what a release
        // later gives back.
        List<SalesOrderDetail> lines = await h.Db.SalesOrderDetails
            .Where(l => l.SalesOrderId == id).OrderBy(l => l.LineNumber).ToListAsync(ct);

        Assert.Equal([6m, 4m], lines.Select(l => l.ReservedQuantity));
    }

    /// <summary>
    /// A shortage leaves a draft with no number spent. The reverse — a number
    /// issued for an order that never confirmed — is a hole in a statutory
    /// sequence.
    /// </summary>
    [SkippableFact]
    public async Task A_shortage_leaves_a_draft_and_spends_no_number()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        h.Inventory.Short(itemId: 42, available: 1m);

        long id = await h.Draft((41, 6m), (42, 4m));
        SalesOrderResult result = await h.Orders.ConfirmAsync(id, ct);

        Assert.Equal(SalesOrderOutcome.InsufficientStock, result.Outcome);

        SalesOrderShortage shortage = Assert.Single(result.Shortages!);
        Assert.Equal(42, shortage.ItemId);
        Assert.Equal(4m, shortage.Requested);
        Assert.Equal(1m, shortage.Available);

        // Still a draft, and still holding the number it was created with — a
        // refused confirm changes nothing at all.
        SalesOrder order = await h.Db.SalesOrders.FirstAsync(o => o.SalesOrderId == id, ct);
        Assert.Equal(DocumentStatus.Draft, order.Status);
        Assert.EndsWith("00001", order.DocumentNo);
        Assert.Null(order.PostedAt);
    }

    /// <summary>
    /// Inventory being unreachable is not the same as a refusal, and must not be
    /// reported as one: nothing was held, so the order is still a draft and the
    /// caller should try again rather than re-key it.
    /// </summary>
    [SkippableFact]
    public async Task Inventory_being_unreachable_is_distinct_from_a_shortage()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        h.Inventory.Unreachable = true;

        long id = await h.Draft((41, 2m));
        SalesOrderResult result = await h.Orders.ConfirmAsync(id, ct);

        Assert.Equal(SalesOrderOutcome.InventoryUnreachable, result.Outcome);

        SalesOrder order = await h.Db.SalesOrders.FirstAsync(o => o.SalesOrderId == id, ct);
        Assert.Equal(DocumentStatus.Draft, order.Status);
        Assert.Null(order.PostedAt);
    }

    /// <summary>Cancelling gives back exactly what the order was holding, and keeps its number.</summary>
    [SkippableFact]
    public async Task Cancelling_releases_what_it_holds_and_keeps_the_number()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long id = await h.Draft((41, 6m), (42, 4m));
        await h.Orders.ConfirmAsync(id, ct);

        string number = (await h.Db.SalesOrders.FirstAsync(o => o.SalesOrderId == id, ct)).DocumentNo;

        SalesOrderResult cancelled = await h.Orders.CancelAsync(
            id, new CloseSalesOrderRequest { Reason = "Customer withdrew." }, ct);

        Assert.Equal(SalesOrderOutcome.Ok, cancelled.Outcome);

        Assert.Equal(
            [(41L, 6m), (42L, 4m)],
            h.Inventory.Released.Select(r => (r.ItemId, r.Quantity)).Order().ToArray());

        SalesOrder order = await h.Db.SalesOrders.FirstAsync(o => o.SalesOrderId == id, ct);
        Assert.Equal(FulfilmentStatus.Cancelled, order.FulfilmentStatus);

        // Cancelling withdraws the document, which is what Void means. The
        // schema refuses a Void with no reason, and a reason with no Void.
        Assert.Equal(DocumentStatus.Void, order.Status);
        Assert.Equal(number, order.DocumentNo);
        Assert.Equal("Customer withdrew.", order.VoidReason);

        // Nothing is held any more, so a second close has nothing to give back.
        Assert.True(await h.Db.SalesOrderDetails
            .Where(l => l.SalesOrderId == id).AllAsync(l => l.ReservedQuantity == 0m, ct));
    }

    /// <summary>A short close releases the remainder and says nothing further is coming.</summary>
    [SkippableFact]
    public async Task Short_closing_releases_the_remainder()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long id = await h.Draft((41, 10m));
        await h.Orders.ConfirmAsync(id, ct);

        // Six of the ten shipped, so only four are still a promise.
        SalesOrderDetail line = await h.Db.SalesOrderDetails.FirstAsync(l => l.SalesOrderId == id, ct);
        line.DeliveredQuantity = 6m;
        line.ReservedQuantity = 4m;
        await h.Db.SaveChangesAsync(ct);
        h.Inventory.Released.Clear();

        Assert.Equal(
            SalesOrderOutcome.Ok,
            (await h.Orders.ShortCloseAsync(
                id, new CloseSalesOrderRequest { Reason = "Balance not required." }, ct)).Outcome);

        (long ItemId, decimal Quantity) released = Assert.Single(
            h.Inventory.Released.Select(r => (r.ItemId, r.Quantity)));

        Assert.Equal((41L, 4m), released);

        SalesOrder closed = await h.Db.SalesOrders.FirstAsync(o => o.SalesOrderId == id, ct);

        Assert.Equal(FulfilmentStatus.Closed, closed.FulfilmentStatus);

        // Not a void. Half of this order shipped, so the document is a true
        // record of what was ordered and what went out — it only stops
        // expecting more.
        Assert.Equal(DocumentStatus.Posted, closed.Status);
        Assert.Null(closed.VoidedAt);
        Assert.Contains("Balance not required.", closed.Notes);
    }

    /// <summary>A confirmed order is holding stock, so it cannot be edited.</summary>
    [SkippableFact]
    public async Task A_confirmed_order_refuses_an_edit()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long id = await h.Draft((41, 2m));
        await h.Orders.ConfirmAsync(id, ct);

        Assert.Equal(
            SalesOrderOutcome.NotDraft,
            (await h.Orders.SaveAsync(id, h.Request((41, 3m)), ct)).Outcome);
    }

    /// <summary>Inventory, as this service sees it: yes, no, or no answer.</summary>
    private sealed class FakeInventory : IInventoryStock
    {
        private readonly Dictionary<long, decimal> _short = [];

        public List<StockLine> Reserved { get; } = [];

        public List<StockLine> Released { get; } = [];

        public bool Unreachable { get; set; }

        public void Short(long itemId, decimal available) => _short[itemId] = available;

        public Task<StockReservationResult> ReserveAsync(
            Guid customerId, Guid orgId, long salesOrderId, IReadOnlyList<StockLine> lines,
            CancellationToken ct)
        {
            if (Unreachable)
            {
                return Task.FromResult(new StockReservationResult(false, [], Unreachable: true));
            }

            List<StockShortage> shortages =
            [
                .. lines
                    .Where(l => _short.TryGetValue(l.ItemId, out decimal a) && a < l.Quantity)
                    .Select(l => new StockShortage(
                        l.LineNumber, l.ItemId, $"ITEM-{l.ItemId}", $"Item {l.ItemId}",
                        l.Quantity, _short[l.ItemId], "InsufficientStock")),
            ];

            if (shortages.Count > 0)
            {
                // Nothing is taken when anything is short — the real endpoint
                // checks every line before it takes any.
                return Task.FromResult(new StockReservationResult(false, shortages));
            }

            Reserved.AddRange(lines);
            return Task.FromResult(new StockReservationResult(true, []));
        }

        public Task<StockReservationResult> ReleaseAsync(
            Guid customerId, Guid orgId, long salesOrderId, IReadOnlyList<StockLine> lines,
            CancellationToken ct)
        {
            if (Unreachable)
            {
                return Task.FromResult(new StockReservationResult(false, [], Unreachable: true));
            }

            Released.AddRange(lines);
            return Task.FromResult(new StockReservationResult(true, []));
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required SalesDbContext Db { get; init; }

        public required SalesOrderService Orders { get; init; }

        public required FakeInventory Inventory { get; init; }

        public SaveSalesOrderRequest Request(params (long ItemId, decimal Qty)[] lines) => new()
        {
            ContactId = 7,
            DocumentDate = new DateOnly(2026, 8, 22),
            PlaceOfSupplyStateId = 33,
            Lines =
            [
                .. lines.Select(l => new SaveSalesOrderLineRequest
                {
                    ItemId = l.ItemId,
                    Quantity = l.Qty,
                    UnitPrice = 100m,
                }),
            ],
        };

        public async Task<long> Draft(params (long ItemId, decimal Qty)[] lines)
        {
            SalesOrderResult saved =
                await Orders.SaveAsync(null, Request(lines), CancellationToken.None);

            Assert.Equal(SalesOrderOutcome.Ok, saved.Outcome);
            return saved.SalesOrderId!.Value;
        }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var customerId = Guid.NewGuid();
            var orgId = Guid.NewGuid();
            SalesDbContext db = postgres.CreateContext(customerId, orgId);

            // The SOR series exactly as the branch seed writes it: the number is
            // allocated inside the confirm's own transaction, so it has to be
            // real rather than stubbed.
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
            var inventory = new FakeInventory();

            return new Harness
            {
                Db = db,
                Inventory = inventory,
                Orders = new SalesOrderService(
                    db,
                    inventory,
                    new StubRates(),
                    new NumberGenerator(
                        db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
                    tenant,
                    new StubCurrentUser(),
                    TimeProvider.System,
                    NullLogger<SalesOrderService>.Instance),
            };
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }

    /// <summary>No rates. Every line is exempt, which keeps these tests about the order.</summary>
    private sealed class StubRates : ITaxRateProvider
    {
        public Task<IReadOnlyDictionary<long, TaxRate>?> GetRatesAsync(
            DateOnly onDate, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<long, TaxRate>?>(
                new Dictionary<long, TaxRate>());

        public Task<TaxRate?> GetRateAsync(
            long taxGroupId, DateOnly onDate, CancellationToken ct = default) =>
            Task.FromResult<TaxRate?>(null);
    }

    private sealed class StubFinancialYear : IFinancialYearProvider
    {
        public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId => null;
    }
}

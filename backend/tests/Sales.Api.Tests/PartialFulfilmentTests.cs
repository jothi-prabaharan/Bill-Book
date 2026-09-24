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
/// An order delivered and billed in parts (T3.6) — the order, the challan and
/// the invoice services over one database and one inventory stub, so what one
/// document does to the order is what the next one sees.
///
/// <b>What TK-78 found.</b> Nothing recorded what had been billed, so an order
/// could not say it was delivered and not invoiced. An invoice raised after a
/// challan issued the challan's goods a second time unless it named the
/// challan, and fulfilling an order counted only what was invoiced, so it did
/// the same. And a short-close after a partial delivery released too little,
/// leaving stock reserved for an order that had closed.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PartialFulfilmentTests
{
    private readonly PostgresFixture _pg;

    public PartialFulfilmentTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task A_challan_for_4_of_10_leaves_the_order_partly_delivered_with_6_held()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        (long orderId, long lineId) = await h.ConfirmOrderAsync(10m);

        await h.DeliverAsync(orderId, lineId, 4m);

        SalesOrder order = await h.ReadOrderAsync(orderId);
        SalesOrderDetail line = order.Lines.Single();
        Assert.Equal(FulfilmentStatus.PartlyDelivered, order.FulfilmentStatus);
        Assert.Equal(4m, line.DeliveredQuantity);
        Assert.Equal(6m, line.ReservedQuantity);
        Assert.Equal(0m, line.InvoicedQuantity);
    }

    [SkippableFact]
    public async Task A_second_challan_for_the_other_6_closes_the_order_with_nothing_held()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        (long orderId, long lineId) = await h.ConfirmOrderAsync(10m);

        await h.DeliverAsync(orderId, lineId, 4m);
        await h.DeliverAsync(orderId, lineId, 6m);

        SalesOrder order = await h.ReadOrderAsync(orderId);
        Assert.Equal(FulfilmentStatus.Closed, order.FulfilmentStatus);
        Assert.Equal(10m, order.Lines.Single().DeliveredQuantity);
        Assert.Equal(0m, order.Lines.Single().ReservedQuantity);
    }

    [SkippableFact]
    public async Task An_invoice_against_the_first_challan_issues_no_stock_and_bills_4()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        (long orderId, long lineId) = await h.ConfirmOrderAsync(10m);
        long challanId = await h.DeliverAsync(orderId, lineId, 4m);
        int issuesBefore = h.Inventory.Issues.Count;

        InvoiceResult posted = await h.InvoiceAsync(orderId, lineId, 4m, deliveryChallanId: challanId);
        Assert.Equal(InvoiceOutcome.Ok, posted.Outcome);

        // The goods left on the challan; the invoice only bills them.
        Assert.Equal(issuesBefore, h.Inventory.Issues.Count);

        SalesOrder order = await h.ReadOrderAsync(orderId);
        SalesOrderDetail line = order.Lines.Single();
        Assert.Equal(4m, line.InvoicedQuantity);
        Assert.Equal(4m, line.DeliveredQuantity);
        Assert.Equal(6m, line.ReservedQuantity);
    }

    [SkippableFact]
    public async Task An_order_delivered_and_billed_in_two_parts_goes_open_partly_delivered_closed()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        (long orderId, long lineId) = await h.ConfirmOrderAsync(10m);
        Assert.Equal(FulfilmentStatus.Open, (await h.ReadOrderAsync(orderId)).FulfilmentStatus);

        // First part: 4 out on a challan, billed against it.
        long first = await h.DeliverAsync(orderId, lineId, 4m);
        Assert.Equal(InvoiceOutcome.Ok, (await h.InvoiceAsync(orderId, lineId, 4m, deliveryChallanId: first)).Outcome);
        Assert.Equal(FulfilmentStatus.PartlyDelivered, (await h.ReadOrderAsync(orderId)).FulfilmentStatus);

        // Second part: the other 6, delivered and billed by fulfilling the rest.
        (InvoiceResult result, FulfillSalesOrderResult? fulfilled) = await h.Invoices.FulfillSalesOrderAsync(
            orderId, new FulfillSalesOrderRequest { DueDate = new DateOnly(2026, 7, 1) }, default);
        Assert.Equal(InvoiceOutcome.Ok, result.Outcome);
        Assert.NotNull(fulfilled);
        Assert.Equal(nameof(FulfilmentStatus.Closed), fulfilled!.Status);
        Assert.Equal(6m, Assert.Single(fulfilled.Lines).FulfilledQuantity);

        SalesOrder order = await h.ReadOrderAsync(orderId);
        SalesOrderDetail line = order.Lines.Single();
        Assert.Equal(FulfilmentStatus.Closed, order.FulfilmentStatus);
        Assert.Equal(10m, line.DeliveredQuantity);
        Assert.Equal(10m, line.InvoicedQuantity);
        Assert.Equal(0m, line.ReservedQuantity);

        SalesOrderViewResult view = await h.Orders.GetAsync(orderId, default);
        Assert.True(view.View!.IsFullyInvoiced);
    }

    [SkippableFact]
    public async Task Fulfilling_after_a_challan_issues_only_what_the_challan_did_not()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        (long orderId, long lineId) = await h.ConfirmOrderAsync(10m);
        await h.DeliverAsync(orderId, lineId, 4m);

        // Bills all 10 without naming the challan — the case that issued the
        // challan's 4 a second time.
        (InvoiceResult result, _) = await h.Invoices.FulfillSalesOrderAsync(
            orderId, new FulfillSalesOrderRequest { DueDate = new DateOnly(2026, 7, 1) }, default);
        Assert.Equal(InvoiceOutcome.Ok, result.Outcome);

        IssueStockRequest invoiceIssue = h.Inventory.Issues.Last();
        Assert.Equal("INV", invoiceIssue.SourceType);
        IssueStockLine issued = Assert.Single(invoiceIssue.Lines);
        Assert.Equal(6m, issued.Quantity);
        Assert.True(issued.ReleaseReservation);

        SalesOrderDetail line = (await h.ReadOrderAsync(orderId)).Lines.Single();
        Assert.Equal(10m, line.DeliveredQuantity);
        Assert.Equal(10m, line.InvoicedQuantity);
        Assert.Equal(0m, line.ReservedQuantity);
    }

    [SkippableFact]
    public async Task Billing_more_than_an_order_line_has_left_is_refused_at_post()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        (long orderId, long lineId) = await h.ConfirmOrderAsync(10m);

        Assert.Equal(InvoiceOutcome.Ok, (await h.InvoiceAsync(orderId, lineId, 8m)).Outcome);

        InvoiceResult second = await h.InvoiceAsync(orderId, lineId, 3m);

        Assert.Equal(InvoiceOutcome.AlreadyFulfilled, second.Outcome);
        Assert.Contains("2 left to bill", second.Detail);
        Assert.Equal(8m, (await h.ReadOrderAsync(orderId)).Lines.Single().InvoicedQuantity);
    }

    [SkippableFact]
    public async Task A_line_naming_an_order_line_of_another_order_is_refused_at_post()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        (long orderId, _) = await h.ConfirmOrderAsync(10m);
        (_, long otherLineId) = await h.ConfirmOrderAsync(5m);

        InvoiceResult result = await h.InvoiceAsync(orderId, otherLineId, 1m);

        Assert.Equal(InvoiceOutcome.LineInvalid, result.Outcome);
    }

    [SkippableFact]
    public async Task Voiding_a_posted_invoice_gives_back_its_billing_and_keeps_its_delivery()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        (long orderId, long lineId) = await h.ConfirmOrderAsync(10m);

        InvoiceResult first = await h.InvoiceAsync(orderId, lineId, 10m);
        Assert.Equal(InvoiceOutcome.Ok, first.Outcome);

        InvoiceResult voided = await h.Invoices.VoidAsync(
            first.InvoiceId, new VoidInvoiceRequest { Reason = "Wrong price" }, default);
        Assert.Equal(InvoiceOutcome.Ok, voided.Outcome);

        SalesOrderDetail line = (await h.ReadOrderAsync(orderId)).Lines.Single();
        Assert.Equal(0m, line.InvoicedQuantity);
        Assert.Equal(10m, line.DeliveredQuantity); // the goods did leave

        // Billing it again issues nothing: the goods are out, only the bill is owed.
        int issuesBefore = h.Inventory.Issues.Count;
        Assert.Equal(InvoiceOutcome.Ok, (await h.InvoiceAsync(orderId, lineId, 10m)).Outcome);
        Assert.Equal(issuesBefore, h.Inventory.Issues.Count);
        Assert.Equal(10m, (await h.ReadOrderAsync(orderId)).Lines.Single().InvoicedQuantity);
    }

    [SkippableFact]
    public async Task Closing_short_after_a_partial_delivery_releases_everything_still_held()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        (long orderId, long lineId) = await h.ConfirmOrderAsync(10m);
        await h.DeliverAsync(orderId, lineId, 4m);

        SalesOrderResult closed = await h.Orders.ShortCloseAsync(
            orderId, new ShortCloseSalesOrderRequest { Reason = "Customer cancelled the rest" }, default);
        Assert.Equal(SalesOrderOutcome.Ok, closed.Outcome);

        // 6 still held; it released 6 − 4 = 2 and kept 4 reserved for ever.
        ReleaseStockRequest release = h.Inventory.Releases.Last();
        Assert.Equal(6m, Assert.Single(release.Lines).Quantity);

        SalesOrder order = await h.ReadOrderAsync(orderId);
        Assert.Equal(FulfilmentStatus.Closed, order.FulfilmentStatus);
        Assert.Equal(0m, order.Lines.Single().ReservedQuantity);

        // Closed short, it can still be billed for what did go out — and only that.
        (InvoiceResult billed, FulfillSalesOrderResult? fulfilled) = await h.Invoices.FulfillSalesOrderAsync(
            orderId, new FulfillSalesOrderRequest { DueDate = new DateOnly(2026, 7, 1) }, default);
        Assert.Equal(InvoiceOutcome.Ok, billed.Outcome);
        Assert.Equal(4m, Assert.Single(fulfilled!.Lines).FulfilledQuantity);
        Assert.Equal(FulfilmentStatus.Closed, (await h.ReadOrderAsync(orderId)).FulfilmentStatus);
    }

    [SkippableFact]
    public async Task Invoicing_an_order_that_was_billed_in_part_bills_the_rest()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        (long orderId, long lineId) = await h.ConfirmOrderAsync(10m);
        Assert.Equal(InvoiceOutcome.Ok, (await h.InvoiceAsync(orderId, lineId, 3m)).Outcome);

        // It refused any order with an invoice at all.
        InvoiceResult rest = await h.Invoices.CreateFromSalesOrderAsync(
            orderId, new CreateInvoiceFromOrderRequest { DueDate = new DateOnly(2026, 7, 1) }, default);
        Assert.Equal(InvoiceOutcome.Ok, rest.Outcome);

        InvoiceDetail line = await h.Db.InvoiceDetails.SingleAsync(d => d.InvoiceId == rest.InvoiceId);
        Assert.Equal(7m, line.Quantity);
        Assert.Equal(lineId, line.SalesOrderDetailId);
    }

    // ── Harness ─────────────────────────────────────────────────────────

    /// <summary>
    /// One branch with its numbering series, and the three services that move
    /// an order — sharing one context, one inventory stub and one ledger stub.
    /// </summary>
    private sealed record Harness(
        SalesDbContext Db,
        SalesOrderService Orders,
        DeliveryChallanService Challans,
        InvoiceService Invoices,
        RecordingInventory Inventory)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            SalesDbContext db = pg.CreateContext(customerId, orgId);
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" };
            StubNameLookup names = new();
            RecordingInventory inventory = new();
            RecordingLedger ledger = new();
            NumberGenerator numbering = new(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            SalesOrderService orders = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(), new StubTaxRates(),
                names, names, new StubCurrentUser(), TimeProvider.System, inventory, new StubCreditCheck());

            DeliveryChallanService challans = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(), new StubTaxRates(),
                names, names, new StubCurrentUser(), TimeProvider.System, inventory, TestArchive.For(db, tenant));

            InvoiceService invoices = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(), new StubTaxRates(),
                names, names, new StubCurrentUser(), TimeProvider.System, inventory, ledger,
                new StubCreditCheck(), new StubDocumentStorage(), new StubInvoicePdf(), new StubOrgIdentity());

            return new Harness(db, orders, challans, invoices, inventory);
        }

        /// <summary>A confirmed order for item 7 to contact 42. Returns the order and its one line.</summary>
        public async Task<(long OrderId, long LineId)> ConfirmOrderAsync(decimal quantity)
        {
            SalesOrderResult created = await Orders.CreateAsync(new SaveSalesOrderRequest
            {
                DocumentDate = new DateOnly(2026, 6, 1),
                ContactId = 42,
                DeliveryDate = new DateOnly(2026, 6, 15),
                PlaceOfSupplyStateCode = "33",
                Lines =
                [
                    new SaveSalesOrderLineRequest
                    {
                        ItemId = 7,
                        Quantity = quantity,
                        ConversionFactor = 1m,
                        UnitPrice = 100m,
                        TaxGroupId = 1,
                        LineType = DocumentLineType.Stock,
                    },
                ],
            }, default);
            Assert.Equal(SalesOrderOutcome.Ok, created.Outcome);
            Assert.Equal(SalesOrderOutcome.Ok, (await Orders.ConfirmAsync(created.SalesOrderId, default)).Outcome);

            long lineId = await Db.Set<SalesOrderDetail>()
                .Where(l => l.SalesOrderId == created.SalesOrderId)
                .Select(l => l.SalesOrderDetailId)
                .SingleAsync();

            return (created.SalesOrderId, lineId);
        }

        /// <summary>A posted challan delivering part of the order's line.</summary>
        public async Task<long> DeliverAsync(long orderId, long lineId, decimal quantity)
        {
            DeliveryChallanResult saved = await Challans.SaveAsync(null, new SaveDeliveryChallanRequest
            {
                SalesOrderId = orderId,
                DocumentDate = new DateOnly(2026, 6, 2),
                DispatchDate = new DateOnly(2026, 6, 2),
                ContactId = 42,
                ContactGstin = "33AAAAA0000A1Z5",
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                Lines =
                [
                    new SaveDeliveryChallanLineRequest
                    {
                        ItemId = 7,
                        Quantity = quantity,
                        UnitPrice = 100m,
                        TaxGroupId = 1,
                        SalesOrderDetailId = lineId,
                    },
                ],
            }, default);
            Assert.Equal(DeliveryChallanOutcome.Ok, saved.Outcome);

            DeliveryChallanResult posted = await Challans.PostAsync(saved.DeliveryChallanId, default);
            Assert.True(posted.Outcome == DeliveryChallanOutcome.Ok, $"{posted.Outcome}: {posted.Detail}");
            return saved.DeliveryChallanId;
        }

        /// <summary>An invoice billing part of the order's line, created and posted.</summary>
        public async Task<InvoiceResult> InvoiceAsync(
            long orderId, long lineId, decimal quantity, long? deliveryChallanId = null)
        {
            InvoiceResult created = await Invoices.CreateAsync(new SaveInvoiceRequest
            {
                DocumentDate = new DateOnly(2026, 6, 3),
                DueDate = new DateOnly(2026, 7, 3),
                ContactId = 42,
                SalesOrderId = orderId,
                DeliveryChallanId = deliveryChallanId,
                PlaceOfSupplyStateCode = "33",
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                Lines =
                [
                    new SaveInvoiceLineRequest
                    {
                        ItemId = 7,
                        Quantity = quantity,
                        ConversionFactor = 1m,
                        UnitPrice = 100m,
                        TaxGroupId = 1,
                        LineType = DocumentLineType.Stock,
                        SalesOrderDetailId = lineId,
                    },
                ],
            }, default);

            if (created.Outcome != InvoiceOutcome.Ok)
            {
                return created;
            }

            return await Invoices.PostAsync(created.InvoiceId, default);
        }

        /// <summary>The order as the database has it now, not as the change tracker remembers it.</summary>
        public async Task<SalesOrder> ReadOrderAsync(long orderId)
        {
            Db.ChangeTracker.Clear();
            return await Db.SalesOrders.Include(o => o.Lines).SingleAsync(o => o.SalesOrderId == orderId);
        }
    }
}

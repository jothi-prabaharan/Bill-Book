using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Purchase.Api.Services;
using Purchase.Entity.Enums;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// The goods receipt, against a real PostgreSQL.
///
/// Stock and the ledger are recorded rather than called: each has its own suite
/// proving it does its own job, and what is being claimed here is what Purchase
/// <i>asks</i> of them — which quantity, at what cost, against which accounts,
/// and what it does with the answer.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class GoodsReceiptServiceTests
{
    private const string InventoryAccount = "Inventory";
    private const string GrniAccount = "Goods Received Not Invoiced";

    private readonly PostgresFixture _pg;

    public GoodsReceiptServiceTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task Only_the_accepted_quantity_becomes_stock()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        // Ten delivered, two refused as damaged.
        GoodsReceiptResult created = await h.Receipts.CreateAsync(
            Request([Line(quantity: 10m, accepted: 8m, rejected: 2m, unitPrice: 100m,
                reason: "Two cartons crushed")]),
            default);

        Assert.Equal(GoodsReceiptOutcome.Ok, created.Outcome);

        GoodsReceiptResult posted =
            await h.Receipts.PostAsync(created.GoodsReceiptId, default);

        Assert.Equal(GoodsReceiptOutcome.Ok, posted.Outcome);

        // Eight, not ten. A rejected carton is a dispute, not inventory.
        ReceiveStockRequest call = Assert.Single(h.Inventory.Calls);
        ReceiveStockLine line = Assert.Single(call.Lines);

        Assert.Equal(8m, line.Quantity);
        Assert.Equal("GRN", call.SourceType);
        Assert.Equal(created.GoodsReceiptId, call.SourceId);

        // The cost layer opens at the net cost per unit — spread over what was
        // delivered, not over what was kept, so refusing a carton does not
        // quietly raise the cost of the ones accepted.
        Assert.Equal(100m, line.UnitCost);
    }

    [SkippableFact]
    public async Task A_receipt_posts_inventory_against_the_clearing_account()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceiptResult created = await h.Receipts.CreateAsync(
            Request(
            [
                Line(quantity: 10m, accepted: 10m, rejected: 0m, unitPrice: 100m),
                Line(quantity: 5m, accepted: 4m, rejected: 1m, unitPrice: 50m,
                    reason: "One short"),
            ]),
            default);

        await h.Receipts.PostAsync(created.GoodsReceiptId, default);

        PostLedgerRequest post = Assert.Single(h.Ledger.Posts);

        Assert.Equal("GRN", post.TransactionTypeCode);
        Assert.Equal(created.GoodsReceiptId, post.TransactionId);

        // 10 × 100 + 4 × 50 = 1200, on both sides.
        Assert.Equal(1200m, h.Ledger.DebitOf(InventoryAccount));
        Assert.Equal(1200m, h.Ledger.CreditOf(GrniAccount));

        // Debit xor credit on every leg — Accounting refuses anything else.
        Assert.All(post.Legs, leg =>
            Assert.True((leg.DebitAmount == 0) != (leg.CreditAmount == 0)));

        // No GST leg. Input credit is claimed against the vendor's invoice
        // number, which a receipt does not have; the tax arrives with the bill.
        Assert.DoesNotContain(post.Legs, leg => leg.LedgerTypeId == 2);
    }

    [SkippableFact]
    public async Task A_partial_receipt_leaves_the_order_partly_open()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrder order = await h.OrderForAsync(quantity: 10m, unitPrice: 100m);
        long orderLineId = order.Lines.Single().PurchaseOrderDetailId;

        // Six of ten arrive.
        SaveGoodsReceiptRequest request = Request(
        [
            Line(quantity: 6m, accepted: 6m, rejected: 0m, unitPrice: 100m,
                orderDetailId: orderLineId),
        ]);

        request.PurchaseOrderId = order.PurchaseOrderId;

        GoodsReceiptResult created = await h.Receipts.CreateAsync(request, default);
        await h.Receipts.PostAsync(created.GoodsReceiptId, default);

        PurchaseOrder after = await h.Db.PurchaseOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.PurchaseOrderId == order.PurchaseOrderId);

        Assert.Equal(6m, after.Lines.Single().ReceivedQuantity);
        Assert.Equal(PurchaseFulfilmentStatus.PartlyReceived, after.FulfilmentStatus);

        // The remaining four arrive; the order closes.
        SaveGoodsReceiptRequest second = Request(
        [
            Line(quantity: 4m, accepted: 4m, rejected: 0m, unitPrice: 100m,
                orderDetailId: orderLineId),
        ]);

        second.PurchaseOrderId = order.PurchaseOrderId;

        GoodsReceiptResult secondCreated = await h.Receipts.CreateAsync(second, default);
        await h.Receipts.PostAsync(secondCreated.GoodsReceiptId, default);

        PurchaseOrder closed = await h.Db.PurchaseOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.PurchaseOrderId == order.PurchaseOrderId);

        Assert.Equal(10m, closed.Lines.Single().ReceivedQuantity);
        Assert.Equal(PurchaseFulfilmentStatus.Closed, closed.FulfilmentStatus);
    }

    [SkippableFact]
    public async Task Rejected_goods_leave_the_order_still_owing()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrder order = await h.OrderForAsync(quantity: 10m, unitPrice: 100m);
        long orderLineId = order.Lines.Single().PurchaseOrderDetailId;

        // All ten delivered, two refused. Eight received, two still owed.
        SaveGoodsReceiptRequest request = Request(
        [
            Line(quantity: 10m, accepted: 8m, rejected: 2m, unitPrice: 100m,
                reason: "Damaged in transit", orderDetailId: orderLineId),
        ]);

        request.PurchaseOrderId = order.PurchaseOrderId;

        GoodsReceiptResult created = await h.Receipts.CreateAsync(request, default);
        await h.Receipts.PostAsync(created.GoodsReceiptId, default);

        PurchaseOrder after = await h.Db.PurchaseOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.PurchaseOrderId == order.PurchaseOrderId);

        Assert.Equal(8m, after.Lines.Single().ReceivedQuantity);
        Assert.Equal(PurchaseFulfilmentStatus.PartlyReceived, after.FulfilmentStatus);
    }

    [SkippableFact]
    public async Task A_rejection_needs_a_reason_and_the_quantities_have_to_add_up()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceiptResult noReason = await h.Receipts.CreateAsync(
            Request([Line(quantity: 10m, accepted: 8m, rejected: 2m, unitPrice: 100m)]),
            default);

        Assert.Equal(GoodsReceiptOutcome.LineInvalid, noReason.Outcome);
        Assert.Contains("reason", noReason.Detail);

        GoodsReceiptResult wrongSum = await h.Receipts.CreateAsync(
            Request([Line(quantity: 10m, accepted: 8m, rejected: 1m, unitPrice: 100m,
                reason: "Miscounted")]),
            default);

        Assert.Equal(GoodsReceiptOutcome.LineInvalid, wrongSum.Outcome);

        // Neither spent a number.
        Assert.Empty(await h.Db.GoodsReceipts.ToListAsync());
    }

    [SkippableFact]
    public async Task An_expense_line_belongs_on_the_bill_not_the_receipt()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SaveGoodsReceiptLineRequest freight =
            Line(quantity: 1m, accepted: 1m, rejected: 0m, unitPrice: 500m);

        freight.LineType = DocumentLineType.Expense;
        freight.AccountId = 55;

        GoodsReceiptResult result = await h.Receipts.CreateAsync(Request([freight]), default);

        Assert.Equal(GoodsReceiptOutcome.LineInvalid, result.Outcome);
        Assert.Contains("bill", result.Detail);
    }

    [SkippableFact]
    public async Task A_ledger_refusal_leaves_a_draft_that_can_be_posted_again()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceiptResult created = await h.Receipts.CreateAsync(
            Request([Line(quantity: 10m, accepted: 10m, rejected: 0m, unitPrice: 100m)]),
            default);

        h.Ledger.RefuseWith = "Goods Received Not Invoiced is not in this branch's chart.";

        GoodsReceiptResult refused =
            await h.Receipts.PostAsync(created.GoodsReceiptId, default);

        Assert.Equal(GoodsReceiptOutcome.PostingRefused, refused.Outcome);

        // Still a draft, and the order untouched: nothing was saved.
        GoodsReceipt draft = await h.Db.GoodsReceipts
            .SingleAsync(r => r.GoodsReceiptId == created.GoodsReceiptId);

        Assert.Equal(DocumentStatus.Draft, draft.Status);
        Assert.Null(draft.PostedAt);

        // Post again. The stock is already recorded and comes back as such, and
        // the posted value is taken off the document rather than out of
        // Inventory's reply — so it is the same 1000, not zero.
        h.Ledger.RefuseWith = null;

        GoodsReceiptResult retried =
            await h.Receipts.PostAsync(created.GoodsReceiptId, default);

        Assert.Equal(GoodsReceiptOutcome.Ok, retried.Outcome);

        // Inventory was asked twice with the same line, and deduped the second —
        // which is exactly why the value below cannot come from its reply.
        Assert.Equal(2, h.Inventory.Calls.Count);
        Assert.Equal(
            h.Inventory.Calls[0].Lines.Select(l => (l.SourceLineId, l.Quantity)),
            h.Inventory.Calls[1].Lines.Select(l => (l.SourceLineId, l.Quantity)));

        Assert.Equal(1000m, h.Ledger.CreditOf(GrniAccount));

        GoodsReceipt after = await h.Db.GoodsReceipts
            .SingleAsync(r => r.GoodsReceiptId == created.GoodsReceiptId);

        Assert.Equal(DocumentStatus.Posted, after.Status);
        Assert.NotNull(after.PostedAt);
    }

    [SkippableFact]
    public async Task Inventory_refusing_leaves_the_ledger_untouched()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceiptResult created = await h.Receipts.CreateAsync(
            Request([Line(quantity: 10m, accepted: 10m, rejected: 0m, unitPrice: 100m)]),
            default);

        h.Inventory.Refuse[ItemId] = "BatchRequired";

        GoodsReceiptResult result =
            await h.Receipts.PostAsync(created.GoodsReceiptId, default);

        Assert.Equal(GoodsReceiptOutcome.StockRefused, result.Outcome);
        Assert.Contains("BatchRequired", result.Detail);

        // Stock is tried first precisely so that a refusal leaves nothing to
        // undo: no posting was attempted and the receipt is still a draft.
        Assert.Empty(h.Ledger.Posts);

        GoodsReceipt draft = await h.Db.GoodsReceipts
            .SingleAsync(r => r.GoodsReceiptId == created.GoodsReceiptId);

        Assert.Equal(DocumentStatus.Draft, draft.Status);
    }

    [SkippableFact]
    public async Task A_posted_receipt_is_neither_edited_nor_voided()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceiptResult created = await h.Receipts.CreateAsync(
            Request([Line(quantity: 4m, accepted: 4m, rejected: 0m, unitPrice: 25m)]),
            default);

        await h.Receipts.PostAsync(created.GoodsReceiptId, default);

        GoodsReceiptResult edit = await h.Receipts.UpdateAsync(
            created.GoodsReceiptId,
            Request([Line(quantity: 9m, accepted: 9m, rejected: 0m, unitPrice: 25m)]),
            default);

        Assert.Equal(GoodsReceiptOutcome.LifecycleRefused, edit.Outcome);

        // Voiding cannot un-receive goods. The refusal says so and points at the
        // debit note, which is the document that actually reverses both halves.
        GoodsReceiptResult voided = await h.Receipts.VoidAsync(
            created.GoodsReceiptId,
            new VoidGoodsReceiptRequest { Reason = "Keyed against the wrong vendor" },
            default);

        Assert.Equal(GoodsReceiptOutcome.LifecycleRefused, voided.Outcome);
        Assert.Contains("debit note", voided.Detail);
    }

    [SkippableFact]
    public async Task A_receipt_against_another_vendors_order_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrder order = await h.OrderForAsync(quantity: 5m, unitPrice: 10m);

        SaveGoodsReceiptRequest request =
            Request([Line(quantity: 5m, accepted: 5m, rejected: 0m, unitPrice: 10m)]);

        request.PurchaseOrderId = order.PurchaseOrderId;
        request.ContactId = 999;

        GoodsReceiptResult result = await h.Receipts.CreateAsync(request, default);

        Assert.Equal(GoodsReceiptOutcome.OrderInvalid, result.Outcome);
        Assert.Contains("different vendor", result.Detail);
    }

    [SkippableFact]
    public async Task A_fully_rejected_delivery_moves_no_stock_and_posts_nothing()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceiptResult created = await h.Receipts.CreateAsync(
            Request([Line(quantity: 6m, accepted: 0m, rejected: 6m, unitPrice: 100m,
                reason: "Wrong specification entirely")]),
            default);

        GoodsReceiptResult posted =
            await h.Receipts.PostAsync(created.GoodsReceiptId, default);

        Assert.Equal(GoodsReceiptOutcome.Ok, posted.Outcome);

        // Nothing arrived, so Inventory was never asked and nothing was posted.
        // The document still exists and still records that the delivery came and
        // was refused, which is the point of raising it.
        Assert.Empty(h.Inventory.Calls);
        Assert.Empty(h.Ledger.Posts);

        GoodsReceipt after = await h.Db.GoodsReceipts
            .SingleAsync(r => r.GoodsReceiptId == created.GoodsReceiptId);

        Assert.Equal(DocumentStatus.Posted, after.Status);
    }

    // ---- Fixtures ---------------------------------------------------------

    private const long ItemId = 7;

    private static SaveGoodsReceiptRequest Request(List<SaveGoodsReceiptLineRequest> lines) =>
        new()
        {
            DocumentDate = new DateOnly(2026, 6, 1),
            ContactId = 42,
            PlaceOfSupplyStateCode = "33",
            VendorDeliveryNoteNo = "DN-1001",
            VendorDeliveryNoteDate = new DateOnly(2026, 5, 31),
            Lines = lines,
        };

    private static SaveGoodsReceiptLineRequest Line(
        decimal quantity,
        decimal accepted,
        decimal rejected,
        decimal unitPrice,
        string? reason = null,
        long? orderDetailId = null) =>
        new()
        {
            ItemId = ItemId,
            PurchaseOrderDetailId = orderDetailId,
            Quantity = quantity,
            AcceptedQuantity = accepted,
            RejectedQuantity = rejected,
            RejectionReason = reason,
            ConversionFactor = 1m,
            UnitPrice = unitPrice,
            LineType = DocumentLineType.Stock,
        };

    private sealed record Harness(
        PurchaseDbContext Db,
        GoodsReceiptService Receipts,
        PurchaseOrderService Orders,
        RecordingInventory Inventory,
        RecordingLedger Ledger)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
            PurchaseDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(
                Repository.SeedData.NumberingSeriesSeed.Build(orgId));

            await db.SaveChangesAsync();

            StubNameLookup names = new();
            RecordingInventory inventory = new();
            RecordingLedger ledger = new();

            var numbering = new NumberGenerator(
                db,
                Options.Create(new NumberingOptions()),
                new StubFinancialYear());

            GoodsReceiptService receipts = new(
                db,
                tenant,
                numbering,
                new StubBaseCurrency(),
                new StubBranchSettings(),
                new StubTaxRates(),
                names,
                names,
                inventory,
                ledger,
                new StubCurrentUser(),
                TimeProvider.System);

            PurchaseOrderService orders = new(
                db,
                numbering,
                new StubBaseCurrency(),
                new StubBranchSettings(),
                new StubTaxRates(),
                names,
                names,
                new StubCurrentUser(),
                TimeProvider.System);

            return new Harness(db, receipts, orders, inventory, ledger);
        }

        /// <summary>An issued order for one item, to receive against.</summary>
        public async Task<PurchaseOrder> OrderForAsync(decimal quantity, decimal unitPrice)
        {
            PurchaseOrderResult created = await Orders.CreateAsync(
                new SavePurchaseOrderRequest
                {
                    DocumentDate = new DateOnly(2026, 5, 1),
                    ContactId = 42,
                    PlaceOfSupplyStateCode = "33",
                    Lines =
                    [
                        new SavePurchaseOrderLineRequest
                        {
                            ItemId = ItemId,
                            Quantity = quantity,
                            ConversionFactor = 1m,
                            UnitPrice = unitPrice,
                            LineType = DocumentLineType.Stock,
                        },
                    ],
                },
                default);

            await Orders.ConfirmAsync(created.PurchaseOrderId, default);

            return await Db.PurchaseOrders
                .Include(o => o.Lines)
                .SingleAsync(o => o.PurchaseOrderId == created.PurchaseOrderId);
        }
    }
}

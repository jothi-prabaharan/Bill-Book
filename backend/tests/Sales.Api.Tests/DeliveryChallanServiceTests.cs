using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Controllers;
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
/// The delivery challan — save, post and void — against a real PostgreSQL, the
/// same discipline <c>SalesOrderServiceTests</c> uses.
///
/// <b>Two bugs in the save path, both only visible from the door the controller
/// uses.</b> <c>SaveAsync</c> never set <c>BaseQuantity</c>, <c>TaxableAmount</c>,
/// <c>GrossAmount</c> or <c>LineNumber</c> on a line, so
/// <c>chk_deliverychallandetails_base_quantity</c> refused every save
/// outright — no delivery challan could be created at all. And the tax split
/// never branched on <c>IsInterState</c>: CGST and SGST were always half the
/// document total, with IGST never populated.
///
/// <b>Three more in post and void, found writing the tests below.</b> Nothing
/// on a saved line named its order line, so posting a challan against an order
/// issued the stock and left the order exactly as it was. Posting wrote a
/// <c>sal.SalesRegister</c> row per line, which GSTR-1 then counted beside the
/// invoice raised from the challan. And voiding a draft subtracted its
/// quantities from an order it had never delivered against.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DeliveryChallanServiceTests
{
    private readonly PostgresFixture _pg;

    public DeliveryChallanServiceTests(PostgresFixture pg) => _pg = pg;

    // ── Save ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task An_intra_state_challan_saves_with_cgst_and_sgst_and_a_real_base_quantity()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        long id = await h.SaveOkAsync(
            Request(contactGstin: "33AAAAA0000A1Z5", [Line(quantity: 10m, unitPrice: 100m)]));

        DeliveryChallan saved = await h.Db.DeliveryChallans
            .Include(x => x.Lines).ThenInclude(l => l.Taxes)
            .SingleAsync(x => x.DeliveryChallanId == id);

        Assert.False(saved.IsInterState);

        // 10 × 100 = 1000 taxable, 18% = 180 split 90 CGST / 90 SGST, total 1180.
        Assert.Equal(1000m, saved.TaxableAmount);
        Assert.Equal(90m, saved.CgstAmount);
        Assert.Equal(90m, saved.SgstAmount);
        Assert.Equal(0m, saved.IgstAmount);
        Assert.Equal(1180m, saved.TotalAmount);

        // The line itself — the four columns SaveAsync used to leave at zero.
        DeliveryChallanDetail line = saved.Lines.Single();
        Assert.Equal(1, line.LineNumber);
        Assert.Equal(10m, line.BaseQuantity);
        Assert.Equal(1000m, line.GrossAmount);
        Assert.Equal(1000m, line.TaxableAmount);

        Assert.Equal(2, line.Taxes.Count);
        Assert.Contains(line.Taxes, t => t.TaxComponent == TaxComponent.Cgst && t.Amount == 90m);
        Assert.Contains(line.Taxes, t => t.TaxComponent == TaxComponent.Sgst && t.Amount == 90m);
    }

    [SkippableFact]
    public async Task An_inter_state_challan_saves_with_igst_only()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        // 07 is Delhi; the branch is seeded at 33 (Tamil Nadu) — see StubBranchSettings.
        long id = await h.SaveOkAsync(
            Request(contactGstin: "07AAAAA0000A1Z5", [Line(quantity: 5m, unitPrice: 200m)]));

        DeliveryChallan saved = await h.Db.DeliveryChallans
            .Include(x => x.Lines).ThenInclude(l => l.Taxes)
            .SingleAsync(x => x.DeliveryChallanId == id);

        Assert.True(saved.IsInterState);
        Assert.Equal(0m, saved.CgstAmount);
        Assert.Equal(0m, saved.SgstAmount);
        Assert.Equal(180m, saved.IgstAmount); // 1000 taxable × 18%
        Assert.Equal(1180m, saved.TotalAmount);

        DeliveryChallanDetailTax tax = Assert.Single(saved.Lines.Single().Taxes);
        Assert.Equal(TaxComponent.Igst, tax.TaxComponent);
        Assert.Equal(180m, tax.Amount);
    }

    [SkippableFact]
    public async Task A_second_line_is_numbered_rather_than_colliding_at_zero()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        long id = await h.SaveOkAsync(
            Request(
                contactGstin: "33AAAAA0000A1Z5",
                [Line(quantity: 1m, unitPrice: 50m), Line(quantity: 2m, unitPrice: 25m)]));

        DeliveryChallan saved = await h.Db.DeliveryChallans
            .Include(x => x.Lines)
            .SingleAsync(x => x.DeliveryChallanId == id);

        Assert.Equal(
            [1, 2], saved.Lines.OrderBy(l => l.LineNumber).Select(l => l.LineNumber));
    }

    [SkippableFact]
    public async Task A_gstin_that_contradicts_the_stated_place_of_supply_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        SaveDeliveryChallanRequest request = Request(
            contactGstin: "33AAAAA0000A1Z5", [Line(1m, 100m)]);
        request.PlaceOfSupplyStateCode = "07"; // disagrees with the GSTIN's own state

        DeliveryChallanResult result = await h.Service.SaveAsync(null, request, default);

        Assert.Equal(DeliveryChallanOutcome.PlaceOfSupplyRefused, result.Outcome);
        Assert.Contains("is registered in state", result.Detail);

        // Refused before a number was taken or a row queued for insert.
        Assert.Equal(0, await h.Db.DeliveryChallans.CountAsync());
    }

    [SkippableFact]
    public async Task A_challan_without_a_currency_takes_the_branchs_own()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        SaveDeliveryChallanRequest request = Request("33AAAAA0000A1Z5", [Line(1m, 100m)]);
        request.CurrencyCode = null;

        long id = await h.SaveOkAsync(request);

        // It defaulted to USD, making every challan keyed without a currency a
        // foreign-currency document.
        DeliveryChallan saved = await h.Db.DeliveryChallans.SingleAsync(x => x.DeliveryChallanId == id);
        Assert.Equal("INR", saved.CurrencyCode);
    }

    [SkippableFact]
    public async Task A_line_on_an_order_challan_that_is_not_an_order_line_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        ConfirmedOrder order = await h.ConfirmOrderAsync(quantity: 10m);

        SaveDeliveryChallanRequest request = Request("33AAAAA0000A1Z5", [Line(4m, 100m)]);
        request.SalesOrderId = order.SalesOrderId; // but the line names no order line

        DeliveryChallanResult result = await h.Service.SaveAsync(null, request, default);

        Assert.Equal(DeliveryChallanOutcome.LineInvalid, result.Outcome);
        Assert.Equal(0, await h.Db.DeliveryChallans.CountAsync());
    }

    [SkippableFact]
    public async Task An_order_line_named_without_an_order_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        ConfirmedOrder order = await h.ConfirmOrderAsync(quantity: 10m);

        SaveDeliveryChallanRequest request = Request(
            "33AAAAA0000A1Z5", [Line(4m, 100m, order.SalesOrderDetailId)]);

        DeliveryChallanResult result = await h.Service.SaveAsync(null, request, default);

        Assert.Equal(DeliveryChallanOutcome.LineInvalid, result.Outcome);
    }

    [SkippableFact]
    public async Task A_challan_against_an_unconfirmed_order_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        ConfirmedOrder draft = await h.ConfirmOrderAsync(quantity: 10m, confirm: false);

        DeliveryChallanResult result = await h.Service.SaveAsync(
            null, AgainstOrder(draft, quantity: 4m), default);

        Assert.Equal(DeliveryChallanOutcome.SourceInvalid, result.Outcome);
    }

    [SkippableFact]
    public async Task A_challan_for_another_customer_than_its_order_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        ConfirmedOrder order = await h.ConfirmOrderAsync(quantity: 10m);

        SaveDeliveryChallanRequest request = AgainstOrder(order, quantity: 4m);
        request.ContactId = 43;

        DeliveryChallanResult result = await h.Service.SaveAsync(null, request, default);

        Assert.Equal(DeliveryChallanOutcome.SourceInvalid, result.Outcome);
    }

    // ── Post ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Posting_against_an_order_moves_its_delivered_and_reserved_quantities()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        ConfirmedOrder order = await h.ConfirmOrderAsync(quantity: 10m);

        long id = await h.SaveOkAsync(AgainstOrder(order, quantity: 4m));

        DeliveryChallanResult posted = await h.Service.PostAsync(id, default);
        Assert.Equal(DeliveryChallanOutcome.Ok, posted.Outcome);

        // The issue asks Inventory to release what the order was holding for
        // this line, not to take the goods on top of the hold.
        IssueStockRequest issue = Assert.Single(h.Inventory.Issues);
        IssueStockLine issued = Assert.Single(issue.Lines);
        Assert.Equal("DLC", issue.SourceType);
        Assert.Equal(id, issue.SourceId);
        Assert.Equal(4m, issued.Quantity);
        Assert.True(issued.ReleaseReservation);

        h.Db.ChangeTracker.Clear();

        SalesOrder saved = await h.Db.SalesOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.SalesOrderId == order.SalesOrderId);

        SalesOrderDetail line = saved.Lines.Single();
        Assert.Equal(4m, line.DeliveredQuantity);
        Assert.Equal(6m, line.ReservedQuantity);
        Assert.Equal(FulfilmentStatus.PartlyDelivered, saved.FulfilmentStatus);

        DeliveryChallan challan = await h.Db.DeliveryChallans.SingleAsync(x => x.DeliveryChallanId == id);
        Assert.Equal(DocumentStatus.Posted, challan.Status);
        Assert.NotNull(challan.PostedAt);
    }

    [SkippableFact]
    public async Task Posting_writes_nothing_to_the_sales_register()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        long id = await h.SaveOkAsync(Request("33AAAAA0000A1Z5", [Line(2m, 100m)]));
        Assert.Equal(DeliveryChallanOutcome.Ok, (await h.Service.PostAsync(id, default)).Outcome);

        // A challan is not a supply under GST; the invoice raised from it is.
        // GSTR-1 reads every register row with no type filter, so a challan's
        // rows were counted a second time beside its invoice's.
        Assert.Equal(0, await h.Db.SalesRegister.CountAsync());
    }

    [SkippableFact]
    public async Task A_challan_cannot_deliver_more_than_its_order_line_has_outstanding()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        ConfirmedOrder order = await h.ConfirmOrderAsync(quantity: 10m);

        long first = await h.SaveOkAsync(AgainstOrder(order, quantity: 7m));
        long second = await h.SaveOkAsync(AgainstOrder(order, quantity: 4m));

        Assert.Equal(DeliveryChallanOutcome.Ok, (await h.Service.PostAsync(first, default)).Outcome);

        // Both were valid drafts; only 3 is left once the first has gone out.
        DeliveryChallanResult refused = await h.Service.PostAsync(second, default);

        Assert.Equal(DeliveryChallanOutcome.OverDelivered, refused.Outcome);
        Assert.Single(h.Inventory.Issues); // the second never reached Inventory
    }

    [SkippableFact]
    public async Task Refused_stock_leaves_the_challan_a_draft_and_its_order_untouched()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        ConfirmedOrder order = await h.ConfirmOrderAsync(quantity: 10m);

        long id = await h.SaveOkAsync(AgainstOrder(order, quantity: 4m));
        h.Inventory.RefuseIssues = true;

        DeliveryChallanResult result = await h.Service.PostAsync(id, default);

        Assert.Equal(DeliveryChallanOutcome.StockRefused, result.Outcome);

        h.Db.ChangeTracker.Clear();

        DeliveryChallan challan = await h.Db.DeliveryChallans.SingleAsync(x => x.DeliveryChallanId == id);
        Assert.Equal(DocumentStatus.Draft, challan.Status);

        SalesOrderDetail line = await h.Db.Set<SalesOrderDetail>()
            .SingleAsync(l => l.SalesOrderDetailId == order.SalesOrderDetailId);
        Assert.Equal(0m, line.DeliveredQuantity);
        Assert.Equal(10m, line.ReservedQuantity);
    }

    [SkippableFact]
    public async Task A_posted_challan_cannot_be_posted_again()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        long id = await h.SaveOkAsync(Request("33AAAAA0000A1Z5", [Line(1m, 100m)]));
        await h.Service.PostAsync(id, default);

        DeliveryChallanResult again = await h.Service.PostAsync(id, default);

        Assert.Equal(DeliveryChallanOutcome.LifecycleRefused, again.Outcome);
        Assert.Single(h.Inventory.Issues);
    }

    [SkippableFact]
    public async Task A_posted_challan_cannot_be_edited()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        SaveDeliveryChallanRequest request = Request("33AAAAA0000A1Z5", [Line(1m, 100m)]);
        long id = await h.SaveOkAsync(request);
        await h.Service.PostAsync(id, default);

        DeliveryChallanResult edit = await h.Service.SaveAsync(id, request, default);

        Assert.Equal(DeliveryChallanOutcome.LifecycleRefused, edit.Outcome);
    }

    // ── Void ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Voiding_a_draft_keeps_the_reason_and_moves_nothing_on_its_order()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        ConfirmedOrder order = await h.ConfirmOrderAsync(quantity: 10m);

        long id = await h.SaveOkAsync(AgainstOrder(order, quantity: 4m));

        DeliveryChallanResult result = await h.Service.VoidAsync(id, "  Keyed twice  ", default);
        Assert.Equal(DeliveryChallanOutcome.Ok, result.Outcome);

        h.Db.ChangeTracker.Clear();

        DeliveryChallan challan = await h.Db.DeliveryChallans.SingleAsync(x => x.DeliveryChallanId == id);
        Assert.Equal(DocumentStatus.Void, challan.Status);
        Assert.Equal("Keyed twice", challan.VoidReason);
        Assert.NotNull(challan.VoidedAt);

        // A draft delivered nothing, so there is nothing to give back. The void
        // used to subtract its quantities from the order anyway.
        SalesOrderDetail line = await h.Db.Set<SalesOrderDetail>()
            .SingleAsync(l => l.SalesOrderDetailId == order.SalesOrderDetailId);
        Assert.Equal(0m, line.DeliveredQuantity);
        Assert.Equal(10m, line.ReservedQuantity);
    }

    [SkippableFact]
    public async Task Voiding_a_posted_challan_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        long id = await h.SaveOkAsync(Request("33AAAAA0000A1Z5", [Line(1m, 100m)]));
        await h.Service.PostAsync(id, default);

        DeliveryChallanResult result = await h.Service.VoidAsync(id, "Wrong customer", default);

        Assert.Equal(DeliveryChallanOutcome.LifecycleRefused, result.Outcome);
        Assert.Contains("Raise a return", result.Detail);

        h.Db.ChangeTracker.Clear();
        DeliveryChallan challan = await h.Db.DeliveryChallans.SingleAsync(x => x.DeliveryChallanId == id);
        Assert.Equal(DocumentStatus.Posted, challan.Status);
    }

    [SkippableFact]
    public async Task A_void_without_a_reason_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        long id = await h.SaveOkAsync(Request("33AAAAA0000A1Z5", [Line(1m, 100m)]));

        DeliveryChallanResult result = await h.Service.VoidAsync(id, "   ", default);

        Assert.Equal(DeliveryChallanOutcome.LifecycleRefused, result.Outcome);
    }

    // ── Another branch ──────────────────────────────────────────────────

    [SkippableFact]
    public async Task Another_branchs_challan_is_not_found_on_every_route()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness mine = await Harness.CreateAsync(_pg);
        Harness theirs = await Harness.CreateAsync(_pg);

        SaveDeliveryChallanRequest request = Request("33AAAAA0000A1Z5", [Line(1m, 100m)]);
        long id = await theirs.SaveOkAsync(request);

        var controller = new DeliveryChallansController(mine.Service);

        // Not 403: under row-level security the service cannot see another
        // branch's row at all, and a 403 would confirm the id exists in someone
        // else's books. Decided by the owner on 23 September 2026 (TK-71).
        Assert.IsType<NotFoundResult>(await controller.Get(id, default));
        Assert.IsType<NotFoundResult>(await controller.Update(id, request, default));
        Assert.IsType<NotFoundResult>(await controller.Post(id, default));
        Assert.IsType<NotFoundResult>(
            await controller.Void(id, new VoidDeliveryChallanRequest { Reason = "Not ours" }, default));

        // And the refusal left their challan alone.
        DeliveryChallan untouched = await theirs.Db.DeliveryChallans.SingleAsync(x => x.DeliveryChallanId == id);
        Assert.Equal(DocumentStatus.Draft, untouched.Status);
        Assert.Empty(mine.Inventory.Issues);
    }

    [SkippableFact]
    public async Task A_refusal_comes_back_with_the_services_own_sentence()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        long id = await h.SaveOkAsync(Request("33AAAAA0000A1Z5", [Line(1m, 100m)]));
        await h.Service.PostAsync(id, default);

        var controller = new DeliveryChallansController(h.Service);

        IActionResult result = await controller.Void(
            id, new VoidDeliveryChallanRequest { Reason = "Wrong customer" }, default);

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(result);
        MessageResponse message = Assert.IsType<MessageResponse>(conflict.Value);
        Assert.Contains("Raise a return", message.Message);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static SaveDeliveryChallanRequest Request(
        string contactGstin, List<SaveDeliveryChallanLineRequest> lines) =>
        new()
        {
            DocumentDate = new DateOnly(2026, 6, 1),
            ContactId = 42,
            ContactGstin = contactGstin,
            DispatchDate = new DateOnly(2026, 6, 1),
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines = lines,
        };

    private static SaveDeliveryChallanLineRequest Line(
        decimal quantity, decimal unitPrice, long? salesOrderDetailId = null) =>
        new()
        {
            ItemId = 7,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxGroupId = 1,
            SalesOrderDetailId = salesOrderDetailId,
        };

    /// <summary>A challan delivering part of the order's one line.</summary>
    private static SaveDeliveryChallanRequest AgainstOrder(ConfirmedOrder order, decimal quantity)
    {
        SaveDeliveryChallanRequest request = Request(
            "33AAAAA0000A1Z5", [Line(quantity, 100m, order.SalesOrderDetailId)]);
        request.SalesOrderId = order.SalesOrderId;
        return request;
    }

    private sealed record ConfirmedOrder(long SalesOrderId, long SalesOrderDetailId);

    /// <summary>
    /// One branch, its numbering series seeded, a challan service and an order
    /// service over the same context, and the inventory stub both of them call.
    /// </summary>
    // ── Archive (TK-22) ─────────────────────────────────────────────────

    [SkippableFact]
    public async Task Posting_a_challan_archives_exactly_one_pdf_naming_its_order()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        ConfirmedOrder order = await h.ConfirmOrderAsync(quantity: 10m);
        long id = await h.SaveOkAsync(AgainstOrder(order, quantity: 4m));

        Assert.Equal(DeliveryChallanOutcome.Ok, (await h.Service.PostAsync(id, default)).Outcome);

        (string key, Shared.Kernel.Storage.FileWriteMode mode) = Assert.Single(h.Storage.Saves);
        Assert.Equal($"0000000042/{h.Db.CurrentOrgId}/retail-erp/sales/delivery-challans/{id}.pdf", key);
        Assert.Equal(Shared.Kernel.Storage.FileWriteMode.Replace, mode);
    }

    [SkippableFact]
    public async Task A_refused_challan_archives_nothing()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        ConfirmedOrder order = await h.ConfirmOrderAsync(quantity: 10m);
        long id = await h.SaveOkAsync(AgainstOrder(order, quantity: 4m));
        h.Inventory.RefuseIssues = true;

        Assert.NotEqual(DeliveryChallanOutcome.Ok, (await h.Service.PostAsync(id, default)).Outcome);
        Assert.Empty(h.Storage.Saves);
    }

    private sealed record Harness(
        SalesDbContext Db,
        DeliveryChallanService Service,
        SalesOrderService Orders,
        RecordingInventory Inventory,
        RecordingDocumentStorage Storage)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            SalesDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            StubNameLookup names = new();
            RecordingInventory inventory = new();
            NumberGenerator numbering = new(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());
            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" };
            RecordingDocumentStorage storage = new();

            DeliveryChallanService service = new(
                db,
                tenant,
                numbering,
                new StubBaseCurrency(),
                new StubBranchSettings(),
                new StubTaxRates(),
                names,
                names,
                new StubCurrentUser(),
                TimeProvider.System,
                inventory,
                TestArchive.For(db, tenant, storage));

            SalesOrderService orders = new(
                db,
                tenant,
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

            return new Harness(db, service, orders, inventory, storage);
        }

        public async Task<long> SaveOkAsync(SaveDeliveryChallanRequest request)
        {
            DeliveryChallanResult result = await Service.SaveAsync(null, request, default);
            Assert.True(
                result.Outcome == DeliveryChallanOutcome.Ok,
                $"Save refused: {result.Outcome} — {result.Detail}");
            return result.DeliveryChallanId;
        }

        /// <summary>An order for one line of item 7 to contact 42, confirmed unless told not to.</summary>
        public async Task<ConfirmedOrder> ConfirmOrderAsync(decimal quantity, bool confirm = true)
        {
            SalesOrderResult created = await Orders.CreateAsync(
                new SaveSalesOrderRequest
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
                },
                default);
            Assert.Equal(SalesOrderOutcome.Ok, created.Outcome);

            if (confirm)
            {
                SalesOrderResult confirmed = await Orders.ConfirmAsync(created.SalesOrderId, default);
                Assert.Equal(SalesOrderOutcome.Ok, confirmed.Outcome);
            }

            long lineId = await Db.Set<SalesOrderDetail>()
                .Where(l => l.SalesOrderId == created.SalesOrderId)
                .Select(l => l.SalesOrderDetailId)
                .SingleAsync();

            return new ConfirmedOrder(created.SalesOrderId, lineId);
        }
    }
}

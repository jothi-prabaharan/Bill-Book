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
/// The purchase order, against a real PostgreSQL.
///
/// <b>Written through the service rather than by inserting rows.</b> A test that
/// hand-built a <c>PurchaseOrder</c> and saved it would be checking its own
/// arithmetic; these go through the door the controller uses, so the numbering
/// allocation, the tax calculator and the check constraints all run.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PurchaseOrderServiceTests
{
    private readonly PostgresFixture _pg;

    public PurchaseOrderServiceTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task A_draft_is_numbered_taxed_and_not_stamped_as_posted()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrderResult result = await h.Service.CreateAsync(
            Request(lines: [Line(quantity: 10m, unitPrice: 100m, taxGroupId: 1)]),
            default);

        Assert.Equal(PurchaseOrderOutcome.Ok, result.Outcome);

        PurchaseOrder saved = await h.Db.PurchaseOrders
            .Include(o => o.Lines)
            .ThenInclude(l => l.Taxes)
            .SingleAsync(o => o.PurchaseOrderId == result.PurchaseOrderId);

        // The number came from the POR series, inside the same transaction.
        Assert.StartsWith("PO/", saved.DocumentNo);
        Assert.Equal("POR", saved.TransactionTypeCode);

        // 10 × 100 = 1000 taxable, 18% = 180, total 1180.
        Assert.Equal(1000m, saved.TaxableAmount);
        Assert.Equal(1180m, saved.TotalAmount);

        // Branch state 33 and no vendor GSTIN resolves intra-state, so the rate
        // splits across CGST and SGST and IGST stays empty.
        Assert.False(saved.IsInterState);
        Assert.Equal(90m, saved.CgstAmount);
        Assert.Equal(90m, saved.SgstAmount);
        Assert.Equal(0m, saved.IgstAmount);

        DocumentLineTaxBase[] taxes = [.. saved.Lines.Single().Taxes];
        Assert.Equal(2, taxes.Length);

        // The one that matters: a draft has never reached the books, so PostedAt
        // is null. The equivalent sales service stamps it on create, which its
        // own chk_salesorders_posted_stamp constraint would refuse.
        Assert.Equal(DocumentStatus.Draft, saved.Status);
        Assert.Null(saved.PostedAt);
        Assert.Null(saved.PostedBy);

        // Nothing has arrived and nothing has been billed.
        Assert.Equal(0m, saved.Lines.Single().ReceivedQuantity);
        Assert.Equal(0m, saved.Lines.Single().BilledQuantity);
        Assert.Equal(PurchaseFulfilmentStatus.Open, saved.FulfilmentStatus);
    }

    [SkippableFact]
    public async Task A_vendor_in_another_state_is_charged_igst()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SavePurchaseOrderRequest request =
            Request(lines: [Line(quantity: 1m, unitPrice: 1000m, taxGroupId: 1)]);

        // 29 is Karnataka; the branch is in 33. With the place of supply left
        // unstated it falls back to where the vendor is registered, which is the
        // ordinary case for a registered vendor.
        request.PlaceOfSupplyStateCode = null;
        request.ContactGstin = "29ABCDE1234F1Z5";

        PurchaseOrderResult result = await h.Service.CreateAsync(request, default);
        Assert.Equal(PurchaseOrderOutcome.Ok, result.Outcome);

        PurchaseOrder saved = await h.Db.PurchaseOrders
            .SingleAsync(o => o.PurchaseOrderId == result.PurchaseOrderId);

        Assert.True(saved.IsInterState);
        Assert.Equal(180m, saved.IgstAmount);
        Assert.Equal(0m, saved.CgstAmount);
        Assert.Equal(0m, saved.SgstAmount);
    }

    [SkippableFact]
    public async Task An_unregistered_vendor_with_no_place_of_supply_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SavePurchaseOrderRequest request =
            Request(lines: [Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]);

        // No GSTIN — an unregistered or composition-scheme vendor — and no
        // stated place of supply. There is then nothing to decide intra against
        // inter from, so the document is refused rather than guessed at. This is
        // more common on the purchase side than the sales side, which is why the
        // form's place-of-supply field matters here.
        request.ContactGstin = null;
        request.PlaceOfSupplyStateCode = null;

        PurchaseOrderResult result = await h.Service.CreateAsync(request, default);

        Assert.Equal(PurchaseOrderOutcome.PlaceOfSupplyRefused, result.Outcome);
        Assert.Empty(await h.Db.PurchaseOrders.ToListAsync());
    }

    [SkippableFact]
    public async Task An_expected_date_before_the_order_date_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SavePurchaseOrderRequest request =
            Request(lines: [Line(quantity: 1m, unitPrice: 10m, taxGroupId: 1)]);

        request.DocumentDate = new DateOnly(2026, 6, 1);
        request.ExpectedDate = new DateOnly(2026, 5, 31);

        PurchaseOrderResult result = await h.Service.CreateAsync(request, default);

        Assert.Equal(PurchaseOrderOutcome.ExpectedDateInvalid, result.Outcome);

        // Refused before the number was taken, so the series stays gapless.
        Assert.Empty(await h.Db.PurchaseOrders.ToListAsync());
    }

    [SkippableFact]
    public async Task Each_line_kind_must_carry_what_it_posts_against()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        // Capital with no fixed asset category: the category owns the GL mapping.
        SavePurchaseOrderLineRequest capital = Line(quantity: 1m, unitPrice: 500m, taxGroupId: 1);
        capital.LineType = DocumentLineType.Capital;

        PurchaseOrderResult capitalResult =
            await h.Service.CreateAsync(Request(lines: [capital]), default);

        Assert.Equal(PurchaseOrderOutcome.LineInvalid, capitalResult.Outcome);
        Assert.Contains("fixed asset category", capitalResult.Detail);

        // Expense with neither item nor account has nowhere to go.
        SavePurchaseOrderLineRequest expense = Line(quantity: 1m, unitPrice: 500m, taxGroupId: 1);
        expense.LineType = DocumentLineType.Expense;
        expense.ItemId = null;
        expense.Description = "Courier";

        PurchaseOrderResult expenseResult =
            await h.Service.CreateAsync(Request(lines: [expense]), default);

        Assert.Equal(PurchaseOrderOutcome.LineInvalid, expenseResult.Outcome);
    }

    [SkippableFact]
    public async Task Confirming_stamps_the_order_and_freezes_it()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrderResult created = await h.Service.CreateAsync(
            Request(lines: [Line(quantity: 2m, unitPrice: 50m, taxGroupId: 1)]),
            default);

        PurchaseOrderResult confirmed =
            await h.Service.ConfirmAsync(created.PurchaseOrderId, default);

        Assert.Equal(PurchaseOrderOutcome.Ok, confirmed.Outcome);

        PurchaseOrder saved = await h.Db.PurchaseOrders
            .SingleAsync(o => o.PurchaseOrderId == created.PurchaseOrderId);

        Assert.Equal(DocumentStatus.Posted, saved.Status);
        Assert.NotNull(saved.PostedAt);
        Assert.NotNull(saved.PostedBy);

        // Frozen: an issued order is not edited. This is the lifecycle refusing,
        // not the database — the message is one a user can act on.
        PurchaseOrderResult edit = await h.Service.UpdateAsync(
            created.PurchaseOrderId,
            Request(lines: [Line(quantity: 5m, unitPrice: 50m, taxGroupId: 1)]),
            default);

        Assert.Equal(PurchaseOrderOutcome.LifecycleRefused, edit.Outcome);
    }

    [SkippableFact]
    public async Task Updating_a_draft_replaces_its_lines_and_refoots_it()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrderResult created = await h.Service.CreateAsync(
            Request(lines:
            [
                Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1),
                Line(quantity: 1m, unitPrice: 200m, taxGroupId: 1),
            ]),
            default);

        PurchaseOrderResult updated = await h.Service.UpdateAsync(
            created.PurchaseOrderId,
            Request(lines: [Line(quantity: 3m, unitPrice: 100m, taxGroupId: 1)]),
            default);

        Assert.Equal(PurchaseOrderOutcome.Ok, updated.Outcome);

        PurchaseOrder saved = await h.Db.PurchaseOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.PurchaseOrderId == created.PurchaseOrderId);

        // One line, renumbered from one — the ledger's ITEM leg keys on the line
        // number, so a replace must not leave a gap.
        PurchaseOrderDetail line = Assert.Single(saved.Lines);
        Assert.Equal(1, line.LineNumber);
        Assert.Equal(300m, saved.TaxableAmount);
        Assert.Equal(354m, saved.TotalAmount);

        // The old lines' tax rows went with them rather than being orphaned.
        Assert.Equal(2, await h.Db.PurchaseOrderDetailTaxes.CountAsync());

        // The number did not change and no second one was taken.
        Assert.Single(await h.Db.PurchaseOrders.ToListAsync());
    }

    [SkippableFact]
    public async Task A_void_needs_a_reason_and_a_received_order_cannot_be_voided()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrderResult created = await h.Service.CreateAsync(
            Request(lines: [Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]),
            default);

        PurchaseOrderResult noReason = await h.Service.VoidAsync(
            created.PurchaseOrderId, new VoidPurchaseOrderRequest { Reason = "  " }, default);

        Assert.Equal(PurchaseOrderOutcome.LifecycleRefused, noReason.Outcome);

        // A receipt against the order makes it un-voidable: withdrawing it would
        // leave the receipt pointing at something that was taken back.
        h.Db.GoodsReceipts.Add(new GoodsReceipt
        {
            TransactionTypeCode = "GRN",
            DocumentNo = "GRN/2627/00001",
            DocumentDate = new DateOnly(2026, 6, 2),
            ContactId = 42,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = DocumentStatus.Draft,
            PurchaseOrderId = created.PurchaseOrderId,
        });

        await h.Db.SaveChangesAsync();

        PurchaseOrderResult received = await h.Service.VoidAsync(
            created.PurchaseOrderId,
            new VoidPurchaseOrderRequest { Reason = "Ordered twice by mistake" },
            default);

        Assert.Equal(PurchaseOrderOutcome.AlreadyReceived, received.Outcome);
    }

    [SkippableFact]
    public async Task Voiding_a_clean_draft_keeps_its_number_and_says_why()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrderResult created = await h.Service.CreateAsync(
            Request(lines: [Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]),
            default);

        PurchaseOrder before = await h.Db.PurchaseOrders
            .AsNoTracking()
            .SingleAsync(o => o.PurchaseOrderId == created.PurchaseOrderId);

        PurchaseOrderResult voided = await h.Service.VoidAsync(
            created.PurchaseOrderId,
            new VoidPurchaseOrderRequest { Reason = "Vendor cannot supply" },
            default);

        Assert.Equal(PurchaseOrderOutcome.Ok, voided.Outcome);

        PurchaseOrder saved = await h.Db.PurchaseOrders
            .SingleAsync(o => o.PurchaseOrderId == created.PurchaseOrderId);

        // The row stays and keeps its number, so the gap in the series is
        // answerable. Never deleted.
        Assert.Equal(before.DocumentNo, saved.DocumentNo);
        Assert.Equal(DocumentStatus.Void, saved.Status);
        Assert.Equal("Vendor cannot supply", saved.VoidReason);
        Assert.NotNull(saved.VoidedAt);
        Assert.Equal(PurchaseFulfilmentStatus.Cancelled, saved.FulfilmentStatus);

        // A void draft is told from a void posting by PostedAt, which is why
        // there is no fifth status.
        Assert.Null(saved.PostedAt);
    }

    [SkippableFact]
    public async Task Unreadable_branch_settings_postpone_the_save_rather_than_guessing()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg, settings: new UnreadableBranchSettings());

        PurchaseOrderResult result = await h.Service.CreateAsync(
            Request(lines: [Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]),
            default);

        // Transient, not wrong: a brief outage should postpone the document
        // rather than commit a figure that was assumed.
        Assert.Equal(PurchaseOrderOutcome.RatesUnavailable, result.Outcome);
        Assert.Empty(await h.Db.PurchaseOrders.ToListAsync());
    }

    [SkippableFact]
    public async Task A_tax_group_with_no_rate_in_force_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        PurchaseOrderResult result = await h.Service.CreateAsync(
            Request(lines:
            [
                Line(quantity: 1m, unitPrice: 100m, taxGroupId: StubTaxRates.UnknownGroupId),
            ]),
            default);

        Assert.Equal(PurchaseOrderOutcome.RatesUnavailable, result.Outcome);
    }

    [SkippableFact]
    public async Task The_list_resolves_every_vendor_name_in_one_call()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        foreach (long contactId in new long[] { 11, 12, 13 })
        {
            SavePurchaseOrderRequest request =
                Request(lines: [Line(quantity: 1m, unitPrice: 100m, taxGroupId: 1)]);

            request.ContactId = contactId;
            await h.Service.CreateAsync(request, default);
        }

        h.Names.Calls.Clear();

        List<PurchaseOrderListItem> rows = await h.Service.ListAsync(default);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.NotNull(row.ContactName));

        // One call for the page, not one per row. The name is deliberately not
        // stored on the document, and that only stays affordable batched.
        IReadOnlyCollection<long> call = Assert.Single(h.Names.Calls);
        Assert.Equal(3, call.Count);
    }

    // ---- Fixtures ---------------------------------------------------------

    /// <summary>
    /// The ordinary order: intra-state, stated explicitly.
    ///
    /// <b>The place of supply is set rather than left to a GSTIN, and that is
    /// the purchase-side default for a reason.</b> A vendor who is unregistered
    /// or on the composition scheme has no GSTIN at all, so on this side of the
    /// product there is often nothing to fall back to — and without either, an
    /// intra-state supply cannot be told from an inter-state one.
    /// </summary>
    private static SavePurchaseOrderRequest Request(List<SavePurchaseOrderLineRequest> lines) =>
        new()
        {
            DocumentDate = new DateOnly(2026, 6, 1),
            ContactId = 42,
            ExpectedDate = new DateOnly(2026, 6, 15),
            PlaceOfSupplyStateCode = "33",
            Lines = lines,
        };

    private static SavePurchaseOrderLineRequest Line(
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
        PurchaseDbContext Db, PurchaseOrderService Service, StubNameLookup Names)
    {
        public static async Task<Harness> CreateAsync(
            PostgresFixture pg, IBranchSettingsProvider? settings = null)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            PurchaseDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(
                Repository.SeedData.NumberingSeriesSeed.Build(orgId));

            await db.SaveChangesAsync();

            StubNameLookup names = new();

            var numbering = new NumberGenerator(
                db,
                Options.Create(new NumberingOptions()),
                new StubFinancialYear());

            PurchaseOrderService service = new(
                db,
                numbering,
                new StubBaseCurrency(),
                settings ?? new StubBranchSettings(),
                new StubTaxRates(),
                names,
                names,
                new StubCurrentUser(),
                TimeProvider.System);

            return new Harness(db, service, names);
        }
    }
}

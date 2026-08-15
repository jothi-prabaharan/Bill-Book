using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Purchase.Api.Services;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// The vendor bill, against a real PostgreSQL.
///
/// The four claims T4.5 makes are each asserted here: a bill against a receipt
/// clears the clearing account to the paise and moves no stock; a bill with no
/// receipt does move stock; a capital line moves neither and lands on the asset
/// account; and payables carry the vendor, which is what makes aging possible.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class BillServiceTests
{
    private const string InventoryAccount = "Inventory";
    private const string GrniAccount = "Goods Received Not Invoiced";
    private const string InputGstAccount = "Input GST";
    private const string AccountsPayableAccount = "Accounts Payable";
    private const string FixedAssetAccount = "Fixed Asset";

    private const long ItemId = 7;

    private readonly PostgresFixture _pg;

    public BillServiceTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task A_bill_against_a_receipt_clears_the_clearing_account_and_moves_no_stock()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        // Ten received at 100 — the receipt credited GRNI 1000.
        GoodsReceipt receipt = await h.PostedReceiptAsync(quantity: 10m, unitPrice: 100m);
        decimal creditedByReceipt = h.Ledger.CreditOf(GrniAccount);

        Assert.Equal(1000m, creditedByReceipt);

        h.Inventory.Calls.Clear();

        GoodsReceiptDetail receiptLine = receipt.Lines.Single();

        BillResult created = await h.Bills.CreateAsync(
            Request(
                receiptId: receipt.GoodsReceiptId,
                lines:
                [
                    Line(quantity: 10m, unitPrice: 100m,
                        receiptDetailId: receiptLine.GoodsReceiptDetailId),
                ]),
            default);

        Assert.Equal(BillOutcome.Ok, created.Outcome);

        BillResult posted = await h.Bills.PostAsync(created.BillId, default);
        Assert.Equal(BillOutcome.Ok, posted.Outcome);

        // To the paise: the bill debits GRNI exactly what the receipt credited.
        Assert.Equal(creditedByReceipt, h.Ledger.DebitOf(GrniAccount));

        // And nothing touched Inventory, because the goods are already there.
        Assert.Empty(h.Inventory.Calls);
        Assert.Equal(0m, h.Ledger.DebitOf(InventoryAccount));
    }

    [SkippableFact]
    public async Task A_bill_with_no_receipt_moves_the_stock_itself()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        BillResult created = await h.Bills.CreateAsync(
            Request(receiptId: null, lines: [Line(quantity: 4m, unitPrice: 250m)]),
            default);

        BillResult posted = await h.Bills.PostAsync(created.BillId, default);
        Assert.Equal(BillOutcome.Ok, posted.Outcome);

        // Nothing came in on a receipt, so the bill does both jobs.
        ReceiveStockRequest call = Assert.Single(h.Inventory.Calls);
        Assert.Equal("BIL", call.SourceType);
        Assert.Equal(created.BillId, call.SourceId);
        Assert.Equal(4m, call.Lines.Single().Quantity);
        Assert.Equal(250m, call.Lines.Single().UnitCost);

        // Inventory is debited directly, and the clearing account is untouched —
        // there is nothing to clear.
        Assert.Equal(1000m, h.Ledger.DebitOf(InventoryAccount));
        Assert.Equal(0m, h.Ledger.DebitOf(GrniAccount));
    }

    [SkippableFact]
    public async Task A_capital_line_moves_no_stock_and_lands_on_the_asset_account()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SaveBillLineRequest laptop = Line(quantity: 1m, unitPrice: 60000m);
        laptop.LineType = DocumentLineType.Capital;
        laptop.FixedAssetCategoryId = 3;

        BillResult created = await h.Bills.CreateAsync(
            Request(receiptId: null, lines: [laptop]), default);

        Assert.Equal(BillOutcome.Ok, created.Outcome);

        BillResult posted = await h.Bills.PostAsync(created.BillId, default);
        Assert.Equal(BillOutcome.Ok, posted.Outcome);

        // Neither: no stock movement, and nothing on Inventory.
        Assert.Empty(h.Inventory.Calls);
        Assert.Equal(0m, h.Ledger.DebitOf(InventoryAccount));

        Assert.Equal(60000m, h.Ledger.DebitOf(FixedAssetAccount));
    }

    [SkippableFact]
    public async Task Payables_carry_the_vendor_so_aging_can_be_grouped()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        BillResult created = await h.Bills.CreateAsync(
            Request(receiptId: null, lines: [Line(quantity: 10m, unitPrice: 100m)]),
            default);

        await h.Bills.PostAsync(created.BillId, default);

        PostLedgerRequest post = h.Ledger.Posts[^1];

        LedgerLegRequest payable = Assert.Single(
            post.Legs, l => l.AccountSystemName == AccountsPayableAccount);

        // 1000 goods + 180 GST at 18%.
        Assert.Equal(1180m, payable.CreditAmount);
        Assert.Equal(0m, payable.DebitAmount);

        // Against the vendor's own sub-account. Without this, payables aging is
        // one number with no vendors in it.
        Assert.Equal(1, payable.SubAccountReferenceType);
        Assert.Equal(42, payable.SubAccountReferenceId);

        // Input GST is a debit — an asset, reclaimable. This is the one place
        // purchase differs in meaning from the identical sales arithmetic.
        Assert.Equal(180m, h.Ledger.DebitOf(InputGstAccount));

        // The whole posting balances.
        Assert.Equal(post.Legs.Sum(l => l.DebitAmount), post.Legs.Sum(l => l.CreditAmount));
    }

    [SkippableFact]
    public async Task The_same_vendor_number_twice_in_a_year_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        await h.Bills.CreateAsync(
            Request(receiptId: null, lines: [Line(quantity: 1m, unitPrice: 100m)]),
            default);

        BillResult duplicate = await h.Bills.CreateAsync(
            Request(receiptId: null, lines: [Line(quantity: 1m, unitPrice: 100m)]),
            default);

        Assert.Equal(BillOutcome.DuplicateVendorBillNo, duplicate.Outcome);
        Assert.Contains("duplicate input tax credit", duplicate.Detail);

        // Refused before a number was spent.
        Assert.Single(await h.Db.Bills.ToListAsync());

        // The next financial year is a different claim and is allowed.
        SaveBillRequest nextYear =
            Request(receiptId: null, lines: [Line(quantity: 1m, unitPrice: 100m)]);

        nextYear.DocumentDate = new DateOnly(2027, 5, 10);
        nextYear.VendorBillDate = new DateOnly(2027, 5, 10);

        BillResult allowed = await h.Bills.CreateAsync(nextYear, default);
        Assert.Equal(BillOutcome.Ok, allowed.Outcome);
    }

    [SkippableFact]
    public async Task A_price_that_disagrees_with_the_receipt_is_refused_rather_than_absorbed()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceipt receipt = await h.PostedReceiptAsync(quantity: 10m, unitPrice: 100m);
        long receiptLineId = receipt.Lines.Single().GoodsReceiptDetailId;

        // Billed at 110 having been received at 100.
        BillResult result = await h.Bills.CreateAsync(
            Request(
                receiptId: receipt.GoodsReceiptId,
                lines: [Line(quantity: 10m, unitPrice: 110m, receiptDetailId: receiptLineId)]),
            default);

        Assert.Equal(BillOutcome.PriceVarianceUndecided, result.Outcome);
        Assert.Contains("purchase price variance", result.Detail, StringComparison.OrdinalIgnoreCase);

        // Nothing was saved: the residue it would have left in the clearing
        // account is exactly what the refusal exists to prevent.
        Assert.Empty(await h.Db.Bills.ToListAsync());
    }

    [SkippableFact]
    public async Task A_bill_cannot_claim_more_than_was_accepted()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        // Ten delivered, two refused — eight accepted.
        GoodsReceipt receipt = await h.PostedReceiptAsync(
            quantity: 10m, unitPrice: 100m, rejected: 2m, reason: "Damaged");

        long receiptLineId = receipt.Lines.Single().GoodsReceiptDetailId;

        BillResult result = await h.Bills.CreateAsync(
            Request(
                receiptId: receipt.GoodsReceiptId,
                lines: [Line(quantity: 10m, unitPrice: 100m, receiptDetailId: receiptLineId)]),
            default);

        Assert.Equal(BillOutcome.LineInvalid, result.Outcome);
        Assert.Contains("only 8", result.Detail);
    }

    [SkippableFact]
    public async Task An_unposted_receipt_has_nothing_to_clear()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceiptResult draft = await h.Receipts.CreateAsync(
            new SaveGoodsReceiptRequest
            {
                DocumentDate = new DateOnly(2026, 6, 1),
                ContactId = 42,
                PlaceOfSupplyStateCode = "33",
                Lines =
                [
                    new SaveGoodsReceiptLineRequest
                    {
                        ItemId = ItemId,
                        Quantity = 5m,
                        AcceptedQuantity = 5m,
                        RejectedQuantity = 0m,
                        ConversionFactor = 1m,
                        UnitPrice = 100m,
                        LineType = DocumentLineType.Stock,
                    },
                ],
            },
            default);

        GoodsReceipt receipt = await h.Db.GoodsReceipts
            .Include(r => r.Lines)
            .SingleAsync(r => r.GoodsReceiptId == draft.GoodsReceiptId);

        BillResult result = await h.Bills.CreateAsync(
            Request(
                receiptId: receipt.GoodsReceiptId,
                lines:
                [
                    Line(quantity: 5m, unitPrice: 100m,
                        receiptDetailId: receipt.Lines.Single().GoodsReceiptDetailId),
                ]),
            default);

        Assert.Equal(BillOutcome.SourceInvalid, result.Outcome);
        Assert.Contains("has not been posted", result.Detail);
    }

    [SkippableFact]
    public async Task A_bill_needs_a_due_date_and_a_payment_term_supplies_one()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        SaveBillRequest noDate =
            Request(receiptId: null, lines: [Line(quantity: 1m, unitPrice: 100m)]);

        noDate.DueDate = null;
        noDate.PaymentTermId = null;

        BillResult refused = await h.Bills.CreateAsync(noDate, default);
        Assert.Equal(BillOutcome.DueDateMissing, refused.Outcome);

        // With a term, the date comes from Accounting rather than being computed
        // here — one implementation of the rule.
        SaveBillRequest withTerm =
            Request(receiptId: null, lines: [Line(quantity: 1m, unitPrice: 100m)]);

        withTerm.DueDate = null;
        withTerm.PaymentTermId = 5;
        h.PaymentTerms.DueDate = new DateOnly(2026, 7, 1);

        BillResult created = await h.Bills.CreateAsync(withTerm, default);
        Assert.Equal(BillOutcome.Ok, created.Outcome);

        Bill saved = await h.Db.Bills.SingleAsync(b => b.BillId == created.BillId);
        Assert.Equal(new DateOnly(2026, 7, 1), saved.DueDate);
    }

    [SkippableFact]
    public async Task Voiding_a_posted_bill_withdraws_its_ledger_rows()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);

        GoodsReceipt receipt = await h.PostedReceiptAsync(quantity: 3m, unitPrice: 100m);
        long receiptLineId = receipt.Lines.Single().GoodsReceiptDetailId;

        BillResult created = await h.Bills.CreateAsync(
            Request(
                receiptId: receipt.GoodsReceiptId,
                lines: [Line(quantity: 3m, unitPrice: 100m, receiptDetailId: receiptLineId)]),
            default);

        await h.Bills.PostAsync(created.BillId, default);

        BillResult voided = await h.Bills.VoidAsync(
            created.BillId, new VoidBillRequest { Reason = "Entered twice" }, default);

        Assert.Equal(BillOutcome.Ok, voided.Outcome);

        // The withdrawal is a posting with no legs naming the types to clear —
        // the rows go rather than being left behind under a void document.
        PostLedgerRequest withdrawal = h.Ledger.Posts[^1];
        Assert.Empty(withdrawal.Legs);
        Assert.NotEmpty(withdrawal.WithdrawLedgerTypeIds);

        Bill after = await h.Db.Bills.SingleAsync(b => b.BillId == created.BillId);
        Assert.Equal(DocumentStatus.Void, after.Status);
        Assert.Equal("Entered twice", after.VoidReason);
    }

    // ---- Fixtures ---------------------------------------------------------

    private static SaveBillRequest Request(long? receiptId, List<SaveBillLineRequest> lines) =>
        new()
        {
            DocumentDate = new DateOnly(2026, 6, 1),
            ContactId = 42,
            GoodsReceiptId = receiptId,
            VendorBillNo = "INV-7",
            VendorBillDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 7, 1),
            PlaceOfSupplyStateCode = "33",
            Lines = lines,
        };

    private static SaveBillLineRequest Line(
        decimal quantity, decimal unitPrice, long? receiptDetailId = null) =>
        new()
        {
            ItemId = ItemId,
            GoodsReceiptDetailId = receiptDetailId,
            Quantity = quantity,
            ConversionFactor = 1m,
            UnitPrice = unitPrice,
            TaxGroupId = 1,
            LineType = DocumentLineType.Stock,
        };

    private sealed record Harness(
        PurchaseDbContext Db,
        BillService Bills,
        GoodsReceiptService Receipts,
        RecordingInventory Inventory,
        RecordingLedger Ledger,
        StubPaymentTerms PaymentTerms)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
            PurchaseDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            StubNameLookup names = new();
            RecordingInventory inventory = new();
            RecordingLedger ledger = new();
            StubPaymentTerms terms = new();

            var numbering = new NumberGenerator(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            GoodsReceiptService receipts = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(),
                new StubTaxRates(), names, names, inventory, ledger,
                new StubCurrentUser(), TimeProvider.System);

            BillService bills = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(),
                new StubTaxRates(), names, names, inventory, ledger, terms,
                new StubCurrentUser(), TimeProvider.System);

            return new Harness(db, bills, receipts, inventory, ledger, terms);
        }

        /// <summary>A posted receipt to bill against.</summary>
        public async Task<GoodsReceipt> PostedReceiptAsync(
            decimal quantity, decimal unitPrice, decimal rejected = 0m, string? reason = null)
        {
            GoodsReceiptResult created = await Receipts.CreateAsync(
                new SaveGoodsReceiptRequest
                {
                    DocumentDate = new DateOnly(2026, 5, 20),
                    ContactId = 42,
                    PlaceOfSupplyStateCode = "33",
                    Lines =
                    [
                        new SaveGoodsReceiptLineRequest
                        {
                            ItemId = ItemId,
                            Quantity = quantity,
                            AcceptedQuantity = quantity - rejected,
                            RejectedQuantity = rejected,
                            RejectionReason = reason,
                            ConversionFactor = 1m,
                            UnitPrice = unitPrice,
                            LineType = DocumentLineType.Stock,
                        },
                    ],
                },
                default);

            await Receipts.PostAsync(created.GoodsReceiptId, default);

            return await Db.GoodsReceipts
                .Include(r => r.Lines)
                .SingleAsync(r => r.GoodsReceiptId == created.GoodsReceiptId);
        }
    }
}

/// <summary>The due date a term implies, without Accounting running.</summary>
public sealed class StubPaymentTerms : IPaymentTermClient
{
    public DateOnly? DueDate { get; set; }

    public Task<DateOnly?> DueDateAsync(
        long paymentTermId,
        Guid customerId,
        Guid orgId,
        DateOnly documentDate,
        CancellationToken ct) => Task.FromResult(DueDate);
}

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
/// The debit note, against a real PostgreSQL.
///
/// What T5.3 claims: <c>Dr Accounts Payable / Cr Purchase Returns</c> with the
/// input GST reversed, and stock back to its layers — but only when the reason
/// is a return.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DebitNoteServiceTests
{
    private const string InventoryAccount = "Inventory";
    private const string PurchaseReturnsAccount = "Purchase Returns";
    private const string InputGstAccount = "Input GST";
    private const string AccountsPayableAccount = "Accounts Payable";

    private const long ItemId = 7;

    private readonly PostgresFixture _pg;

    public DebitNoteServiceTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task A_return_debits_payables_and_credits_purchase_returns()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        Bill bill = await h.PostedBillAsync(quantity: 10m, unitPrice: 100m);

        DebitNoteResult created = await h.Notes.CreateAsync(
            Request(
                bill,
                DebitNoteReason.PurchaseReturn,
                [Line(bill, quantity: 4m, unitPrice: 100m)]),
            default);

        Assert.Equal(DebitNoteOutcome.Ok, created.Outcome);

        DebitNoteResult posted = await h.Notes.PostAsync(created.DebitNoteId, default);
        Assert.Equal(DebitNoteOutcome.Ok, posted.Outcome);

        // 4 × 100 = 400 goods, 72 tax at 18%, 472 owed back.
        Assert.Equal(472m, h.Ledger.DebitOf(AccountsPayableAccount));

        // Purchase Returns is credited by what was bought back. It is a contra
        // Expense, so a report subtracts it rather than adding a negative.
        Assert.Equal(400m, h.Ledger.CreditOf(PurchaseReturnsAccount));

        // Input GST reversed — a credit. Credit cannot be claimed on goods that
        // went back, so this undoes the claim rather than making another.
        Assert.Equal(72m, h.Ledger.CreditOf(InputGstAccount));

        // The whole posting balances.
        PostLedgerRequest post = h.Ledger.Posts[^1];
        Assert.Equal(post.Legs.Sum(l => l.DebitAmount), post.Legs.Sum(l => l.CreditAmount));

        // Against the vendor's own sub-account, so the reduction lands on the
        // right vendor's aging.
        LedgerLegRequest payable = Assert.Single(
            post.Legs, l => l.AccountSystemName == AccountsPayableAccount);

        Assert.Equal(1, payable.SubAccountReferenceType);
        Assert.Equal(42, payable.SubAccountReferenceId);
    }

    [SkippableFact]
    public async Task Stock_goes_back_at_what_its_layers_were_carrying()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        Bill bill = await h.PostedBillAsync(quantity: 10m, unitPrice: 100m);

        // The layers say 90 even though the bill charged 100 — a later receipt
        // moved the weighted average.
        h.Inventory.ReturnUnitCost = 90m;

        DebitNoteResult created = await h.Notes.CreateAsync(
            Request(
                bill,
                DebitNoteReason.PurchaseReturn,
                [Line(bill, quantity: 4m, unitPrice: 100m)]),
            default);

        await h.Notes.PostAsync(created.DebitNoteId, default);

        ReturnStockRequest call = Assert.Single(h.Inventory.Returns);
        Assert.Equal("DBN", call.SourceType);
        Assert.Equal(created.DebitNoteId, call.SourceId);
        Assert.Equal(4m, call.Lines.Single().Quantity);

        // Inventory is credited by what the units were carrying, not by what the
        // bill charged — the layer and the ledger agree about what left.
        Assert.Equal(360m, h.Ledger.CreditOf(InventoryAccount));

        // And the posting still balances, with the difference landing on the
        // account whose job is what a return did to the cost of buying.
        PostLedgerRequest post = h.Ledger.Posts[^1];
        Assert.Equal(post.Legs.Sum(l => l.DebitAmount), post.Legs.Sum(l => l.CreditAmount));
    }

    [SkippableFact]
    public async Task A_price_correction_moves_money_only()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        Bill bill = await h.PostedBillAsync(quantity: 10m, unitPrice: 100m);

        DebitNoteResult created = await h.Notes.CreateAsync(
            Request(
                bill,
                DebitNoteReason.PriceCorrection,
                [Line(bill, quantity: 10m, unitPrice: 5m)]),
            default);

        DebitNoteResult posted = await h.Notes.PostAsync(created.DebitNoteId, default);
        Assert.Equal(DebitNoteOutcome.Ok, posted.Outcome);

        // The goods stayed on the shelf. Moving stock for an overcharge would
        // take away inventory the branch still has.
        Assert.Empty(h.Inventory.Returns);
        Assert.Equal(0m, h.Ledger.CreditOf(InventoryAccount));

        // The money still moves: 50 overcharged plus its 9 of tax.
        Assert.Equal(59m, h.Ledger.DebitOf(AccountsPayableAccount));
        Assert.Equal(50m, h.Ledger.CreditOf(PurchaseReturnsAccount));
        Assert.Equal(9m, h.Ledger.CreditOf(InputGstAccount));
    }

    [SkippableFact]
    public async Task Returning_more_than_was_billed_is_refused()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        Bill bill = await h.PostedBillAsync(quantity: 10m, unitPrice: 100m);

        DebitNoteResult tooMuch = await h.Notes.CreateAsync(
            Request(
                bill,
                DebitNoteReason.PurchaseReturn,
                [Line(bill, quantity: 11m, unitPrice: 100m)]),
            default);

        Assert.Equal(DebitNoteOutcome.OverReturned, tooMuch.Outcome);
        Assert.Contains("input tax twice", tooMuch.Detail);

        // Two lines against the same bill line cannot each take the whole
        // remaining quantity either.
        DebitNoteResult split = await h.Notes.CreateAsync(
            Request(
                bill,
                DebitNoteReason.PurchaseReturn,
                [
                    Line(bill, quantity: 6m, unitPrice: 100m),
                    Line(bill, quantity: 6m, unitPrice: 100m),
                ]),
            default);

        Assert.Equal(DebitNoteOutcome.OverReturned, split.Outcome);
        Assert.Empty(await h.Db.DebitNotes.ToListAsync());
    }

    [SkippableFact]
    public async Task Posting_advances_the_bills_returned_quantity()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        Bill bill = await h.PostedBillAsync(quantity: 10m, unitPrice: 100m);

        DebitNoteResult first = await h.Notes.CreateAsync(
            Request(
                bill,
                DebitNoteReason.PurchaseReturn,
                [Line(bill, quantity: 4m, unitPrice: 100m)]),
            default);

        await h.Notes.PostAsync(first.DebitNoteId, default);

        Bill after = await h.Db.Bills
            .Include(b => b.Lines)
            .SingleAsync(b => b.BillId == bill.BillId);

        Assert.Equal(4m, after.Lines.Single().ReturnedQuantity);

        // Six left, so seven is refused and six is allowed.
        DebitNoteResult tooMuch = await h.Notes.CreateAsync(
            Request(
                after,
                DebitNoteReason.PurchaseReturn,
                [Line(after, quantity: 7m, unitPrice: 100m)]),
            default);

        Assert.Equal(DebitNoteOutcome.OverReturned, tooMuch.Outcome);

        DebitNoteResult rest = await h.Notes.CreateAsync(
            Request(
                after,
                DebitNoteReason.PurchaseReturn,
                [Line(after, quantity: 6m, unitPrice: 100m)]),
            default);

        Assert.Equal(DebitNoteOutcome.Ok, rest.Outcome);
    }

    [SkippableFact]
    public async Task A_draft_bill_has_nothing_to_debit_back()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        Bill bill = await h.DraftBillAsync(quantity: 5m, unitPrice: 100m);

        DebitNoteResult result = await h.Notes.CreateAsync(
            Request(
                bill,
                DebitNoteReason.PurchaseReturn,
                [Line(bill, quantity: 1m, unitPrice: 100m)]),
            default);

        Assert.Equal(DebitNoteOutcome.BillInvalid, result.Outcome);
        Assert.Contains("has not been posted", result.Detail);
    }

    [SkippableFact]
    public async Task Inventory_refusing_leaves_the_ledger_untouched()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        Bill bill = await h.PostedBillAsync(quantity: 10m, unitPrice: 100m);

        DebitNoteResult created = await h.Notes.CreateAsync(
            Request(
                bill,
                DebitNoteReason.PurchaseReturn,
                [Line(bill, quantity: 4m, unitPrice: 100m)]),
            default);

        h.Ledger.Posts.Clear();
        h.Inventory.Refuse[ItemId] = "InsufficientStock";

        DebitNoteResult result = await h.Notes.PostAsync(created.DebitNoteId, default);

        Assert.Equal(DebitNoteOutcome.StockRefused, result.Outcome);
        Assert.Contains("InsufficientStock", result.Detail);

        // Stock is tried first so a refusal leaves nothing to undo.
        Assert.Empty(h.Ledger.Posts);

        DebitNote draft = await h.Db.DebitNotes
            .SingleAsync(n => n.DebitNoteId == created.DebitNoteId);

        Assert.Equal(DocumentStatus.Draft, draft.Status);
    }

    [SkippableFact]
    public async Task A_posted_return_cannot_be_voided_but_a_money_only_note_can()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        Bill bill = await h.PostedBillAsync(quantity: 10m, unitPrice: 100m);

        DebitNoteResult returned = await h.Notes.CreateAsync(
            Request(
                bill,
                DebitNoteReason.PurchaseReturn,
                [Line(bill, quantity: 2m, unitPrice: 100m)]),
            default);

        await h.Notes.PostAsync(returned.DebitNoteId, default);

        DebitNoteResult refused = await h.Notes.VoidAsync(
            returned.DebitNoteId,
            new VoidDebitNoteRequest { Reason = "Keyed in error" },
            default);

        Assert.Equal(DebitNoteOutcome.LifecycleRefused, refused.Outcome);
        Assert.Contains("stock has left", refused.Detail);

        // A money-only note can be voided: withdrawing its rows puts the payable
        // back exactly as it was.
        Bill after = await h.Db.Bills
            .Include(b => b.Lines)
            .SingleAsync(b => b.BillId == bill.BillId);

        DebitNoteResult money = await h.Notes.CreateAsync(
            Request(
                after,
                DebitNoteReason.PriceCorrection,
                [Line(after, quantity: 1m, unitPrice: 10m)]),
            default);

        await h.Notes.PostAsync(money.DebitNoteId, default);

        DebitNoteResult voided = await h.Notes.VoidAsync(
            money.DebitNoteId,
            new VoidDebitNoteRequest { Reason = "Agreed with the vendor instead" },
            default);

        Assert.Equal(DebitNoteOutcome.Ok, voided.Outcome);

        PostLedgerRequest withdrawal = h.Ledger.Posts[^1];
        Assert.Empty(withdrawal.Legs);
        Assert.NotEmpty(withdrawal.WithdrawLedgerTypeIds);
    }

    // ---- Fixtures ---------------------------------------------------------

    private static SaveDebitNoteRequest Request(
        Bill bill, DebitNoteReason reason, List<SaveDebitNoteLineRequest> lines) =>
        new()
        {
            DocumentDate = new DateOnly(2026, 6, 10),
            ContactId = bill.ContactId,
            BillId = bill.BillId,
            ReasonCode = reason,
            PlaceOfSupplyStateCode = "33",
            Lines = lines,
        };

    private static SaveDebitNoteLineRequest Line(
        Bill bill, decimal quantity, decimal unitPrice) =>
        new()
        {
            BillDetailId = bill.Lines.First().BillDetailId,
            ItemId = ItemId,
            Quantity = quantity,
            ConversionFactor = 1m,
            UnitPrice = unitPrice,
            TaxGroupId = 1,
            LineType = DocumentLineType.Stock,
        };

    private sealed record Harness(
        PurchaseDbContext Db,
        DebitNoteService Notes,
        BillService Bills,
        RecordingInventory Inventory,
        RecordingLedger Ledger)
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

            var numbering = new NumberGenerator(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            BillService bills = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(),
                new StubTaxRates(), names, names, inventory, ledger, new StubPaymentTerms(),
                new StubCurrentUser(), TimeProvider.System);

            DebitNoteService notes = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(),
                new StubTaxRates(), names, names, inventory, ledger,
                new StubCurrentUser(), TimeProvider.System);

            return new Harness(db, notes, bills, inventory, ledger);
        }

        public async Task<Bill> DraftBillAsync(decimal quantity, decimal unitPrice)
        {
            BillResult created = await Bills.CreateAsync(
                new SaveBillRequest
                {
                    DocumentDate = new DateOnly(2026, 6, 1),
                    ContactId = 42,
                    VendorBillNo = $"INV-{Guid.NewGuid():N}"[..12],
                    VendorBillDate = new DateOnly(2026, 6, 1),
                    DueDate = new DateOnly(2026, 7, 1),
                    PlaceOfSupplyStateCode = "33",
                    Lines =
                    [
                        new SaveBillLineRequest
                        {
                            ItemId = ItemId,
                            Quantity = quantity,
                            ConversionFactor = 1m,
                            UnitPrice = unitPrice,
                            TaxGroupId = 1,
                            LineType = DocumentLineType.Stock,
                        },
                    ],
                },
                default);

            return await Db.Bills
                .Include(b => b.Lines)
                .SingleAsync(b => b.BillId == created.BillId);
        }

        public async Task<Bill> PostedBillAsync(decimal quantity, decimal unitPrice)
        {
            Bill bill = await DraftBillAsync(quantity, unitPrice);
            await Bills.PostAsync(bill.BillId, default);

            Inventory.Calls.Clear();
            Ledger.Posts.Clear();

            return await Db.Bills
                .Include(b => b.Lines)
                .SingleAsync(b => b.BillId == bill.BillId);
        }
    }
}

using Purchase.Repository.Clients;
using Purchase.Repository.Repositories;
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
/// The purchase GST register, reverse charge and ITC eligibility (TK-115,
/// D-30, D-31). The first test is the card's: a posted bill writes its
/// register rows. The stub rate is 18%, intra-state, so a 1,000 line carries
/// CGST 90 and SGST 90.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PurchaseRegisterTests
{
    private const string InventoryAccount = "Inventory";
    private const string InputGstAccount = "Input GST";
    private const string AccountsPayableAccount = "Accounts Payable";
    private const long ItemId = 7;

    private readonly PostgresFixture _pg;

    public PurchaseRegisterTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task A_posted_bill_writes_its_register_rows()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        BillResult created = await h.Bills.CreateAsync(Request([Line(10m, 100m)]), default);
        Assert.Equal(BillOutcome.Ok, (await h.Bills.PostAsync(created.BillId, default)).Outcome);

        PurchaseRegister row = Assert.Single(await h.Db.PurchaseRegister.AsNoTracking()
            .Where(r => r.SourceId == created.BillId).ToListAsync());

        Assert.Equal("BIL", row.TransactionTypeCode);
        Assert.Equal("INV-7", row.SupplierInvoiceNo);
        Assert.Equal(new DateOnly(2026, 6, 1), row.SupplierInvoiceDate);
        Assert.Equal(1000m, row.TaxableAmount);
        Assert.Equal(90m, row.CgstAmount);
        Assert.Equal(90m, row.SgstAmount);
        Assert.Equal(0m, row.IgstAmount);
        Assert.Equal(18m, row.GstRate);
        Assert.Equal(180m, row.TaxAmountBase);
        Assert.Equal(ItcEligibility.Eligible, row.ItcEligibility);
        Assert.False(row.ReverseCharge);

    }

    [SkippableFact]
    public async Task A_reverse_charge_bill_owes_the_vendor_the_net_and_the_government_the_tax()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        SaveBillRequest request = Request([Line(10m, 100m)]);
        request.IsReverseCharge = true;

        BillResult created = await h.Bills.CreateAsync(request, default);
        Bill bill = await h.Db.Bills.AsNoTracking().SingleAsync(b => b.BillId == created.BillId);
        Assert.Equal(1000m, bill.TotalAmount);
        Assert.Equal(90m, bill.CgstAmount);

        Assert.Equal(BillOutcome.Ok, (await h.Bills.PostAsync(created.BillId, default)).Outcome);

        Assert.Equal(1000m, h.Ledger.CreditOf(AccountsPayableAccount));
        Assert.Equal(180m, h.Ledger.CreditOf(BillRepository.ReverseChargeAccount));
        Assert.Equal(180m, h.Ledger.DebitOf(InputGstAccount));
        AssertBalanced(h.Ledger.Posts[^1]);

        PurchaseRegister row = await h.Db.PurchaseRegister.AsNoTracking().SingleAsync(r => r.SourceId == created.BillId);
        Assert.True(row.ReverseCharge);
    }

    [SkippableFact]
    public async Task Blocked_credit_on_stock_goes_into_its_cost_and_not_into_input_gst()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg);
        SaveBillLineRequest line = Line(10m, 100m);
        line.ItcEligibility = ItcEligibility.Blocked;

        BillResult created = await h.Bills.CreateAsync(Request([line]), default);
        Assert.Equal(BillOutcome.Ok, (await h.Bills.PostAsync(created.BillId, default)).Outcome);

        Assert.Equal(0m, h.Ledger.DebitOf(InputGstAccount));
        Assert.Equal(1180m, h.Ledger.DebitOf(InventoryAccount));
        Assert.Equal(1180m, h.Ledger.CreditOf(AccountsPayableAccount));
        Assert.Equal(118m, Assert.Single(h.Inventory.Calls).Lines.Single().UnitCost);
        AssertBalanced(h.Ledger.Posts[^1]);

        PurchaseRegister row = await h.Db.PurchaseRegister.AsNoTracking().SingleAsync(r => r.SourceId == created.BillId);
        Assert.Equal(ItcEligibility.Blocked, row.ItcEligibility);
    }

    [Fact]
    public void Ineligible_credit_on_an_expense_line_is_part_of_the_expense()
    {
        Bill bill = Bill(isReverseCharge: false, Detail(1, DocumentLineType.Expense, ItcEligibility.Ineligible));

        List<LedgerLegRequest> legs = BillRepository.BuildLegs(bill, againstReceipt: false);

        LedgerLegRequest expense = Assert.Single(legs, l => l.TransactionDetailId == 1 && l.AccountSystemName is null);
        Assert.Equal(1180m, expense.DebitAmount);
        Assert.DoesNotContain(legs, l => l.AccountSystemName == InputGstAccount);
        AssertBalanced(legs);
    }

    [Fact]
    public void Against_a_receipt_blocked_credit_goes_to_stock_and_the_clearing_account_takes_only_the_taxable_value()
    {
        Bill bill = Bill(isReverseCharge: false, Detail(1, DocumentLineType.Stock, ItcEligibility.Blocked));

        List<LedgerLegRequest> legs = BillRepository.BuildLegs(bill, againstReceipt: true);

        Assert.Equal(1000m, legs.Where(l => l.AccountSystemName == "Goods Received Not Invoiced").Sum(l => l.DebitAmount));
        Assert.Equal(180m, legs.Where(l => l.AccountSystemName == InventoryAccount).Sum(l => l.DebitAmount));
        AssertBalanced(legs);
    }

    [Theory]
    [InlineData(false, ItcEligibility.Eligible)]
    [InlineData(true, ItcEligibility.Eligible)]
    [InlineData(true, ItcEligibility.Blocked)]
    [InlineData(false, ItcEligibility.Ineligible)]
    public void Every_combination_of_reverse_charge_and_eligibility_balances(bool reverseCharge, ItcEligibility eligibility)
    {
        Bill bill = Bill(reverseCharge,
            Detail(1, DocumentLineType.Stock, eligibility),
            Detail(2, DocumentLineType.Capital, ItcEligibility.Eligible));

        AssertBalanced(BillRepository.BuildLegs(bill, againstReceipt: false));

        // And a debit note for the first line reverses it, balanced too.
        DebitNote note = new()
        {
            DebitNoteId = 9,
            BillId = bill.BillId,
            ContactId = bill.ContactId,
            DocumentNo = "DBN-1",
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            TotalAmount = reverseCharge ? 1000m : 1180m,
            Lines =
            [
                new DebitNoteDetail
                {
                    DebitNoteDetailId = 1,
                    BillDetailId = 1,
                    LineNumber = 1,
                    Quantity = 10m,
                    TaxableAmount = 1000m,
                    Taxes = [Tax<DebitNoteDetailTax>(TaxComponent.Cgst), Tax<DebitNoteDetailTax>(TaxComponent.Sgst)],
                },
            ],
        };

        List<LedgerLegRequest> reversal = DebitNoteRepository.BuildLegs(note, 0m, bill);
        AssertBalanced(reversal);
        Assert.Equal(reverseCharge ? 180m : 0m, reversal.Where(l => l.AccountSystemName == BillRepository.ReverseChargeAccount).Sum(l => l.DebitAmount));
        Assert.Equal(eligibility == ItcEligibility.Eligible ? 180m : 0m, reversal.Where(l => l.AccountSystemName == InputGstAccount).Sum(l => l.CreditAmount));
    }

    [Fact]
    public void A_debit_note_row_names_the_bill_and_keeps_its_lines_eligibility()
    {
        Bill bill = Bill(isReverseCharge: true, Detail(1, DocumentLineType.Stock, ItcEligibility.Blocked));
        DebitNote note = new()
        {
            DebitNoteId = 9,
            BillId = bill.BillId,
            ContactId = bill.ContactId,
            DocumentNo = "DBN-1",
            DocumentDate = new DateOnly(2026, 6, 10),
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines = [new DebitNoteDetail { DebitNoteDetailId = 4, BillDetailId = 1, LineNumber = 1, Quantity = 2m, TaxableAmount = 200m, Taxes = [] }],
        };

        PurchaseRegister row = Assert.Single(PurchaseRegisterRowsRepository.ForDebitNote(note, bill));

        Assert.Equal("DBN", row.TransactionTypeCode);
        Assert.Equal(bill.BillId, row.OriginalBillId);
        Assert.Equal("INV-7", row.SupplierInvoiceNo);
        Assert.Equal(ItcEligibility.Blocked, row.ItcEligibility);
        Assert.True(row.ReverseCharge);
    }

    private static void AssertBalanced(PostLedgerRequest request) => AssertBalanced(request.Legs);

    private static void AssertBalanced(IEnumerable<LedgerLegRequest> legs)
    {
        List<LedgerLegRequest> all = [.. legs];
        Assert.Equal(all.Sum(l => l.DebitAmount), all.Sum(l => l.CreditAmount));
    }

    private static Bill Bill(bool isReverseCharge, params BillDetail[] lines) => new()
    {
        BillId = 5,
        ContactId = 42,
        DocumentNo = "BIL-5",
        VendorBillNo = "INV-7",
        VendorBillDate = new DateOnly(2026, 6, 1),
        DocumentDate = new DateOnly(2026, 6, 1),
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        IsReverseCharge = isReverseCharge,
        Lines = [.. lines],
    };

    private static BillDetail Detail(long id, DocumentLineType type, ItcEligibility eligibility) => new()
    {
        BillDetailId = id,
        LineNumber = (int)id,
        LineType = type,
        ItemId = type == DocumentLineType.Stock ? ItemId : null,
        AccountId = type == DocumentLineType.Expense ? 61 : null,
        FixedAssetCategoryId = type == DocumentLineType.Capital ? 3 : null,
        Quantity = 10m,
        TaxableAmount = 1000m,
        TaxMasterId = 1,
        ItcEligibility = eligibility,
        Taxes = [Tax<BillDetailTax>(TaxComponent.Cgst), Tax<BillDetailTax>(TaxComponent.Sgst)],
    };

    private static T Tax<T>(TaxComponent component) where T : DocumentLineTaxBase, new() =>
        new() { TaxComponent = component, Rate = 9m, TaxableAmount = 1000m, Amount = 90m };

    private static SaveBillRequest Request(List<SaveBillLineRequest> lines) => new()
    {
        DocumentDate = new DateOnly(2026, 6, 1),
        ContactId = 42,
        VendorBillNo = "INV-7",
        VendorBillDate = new DateOnly(2026, 6, 1),
        DueDate = new DateOnly(2026, 7, 1),
        PlaceOfSupplyStateCode = "33",
        Lines = lines,
    };

    private static SaveBillLineRequest Line(decimal quantity, decimal unitPrice) => new()
    {
        ItemId = ItemId,
        Quantity = quantity,
        ConversionFactor = 1m,
        UnitPrice = unitPrice,
        TaxGroupId = 1,
        LineType = DocumentLineType.Stock,
    };

    private sealed record Harness(PurchaseDbContext Db, BillRepository Bills, RecordingInventory Inventory, RecordingLedger Ledger)
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
            var numbering = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            BillRepository bills = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(),
                new StubTaxRates(), names, names, inventory, ledger, new StubPaymentTerms(), new RecordingFixedAssets(),
                new StubCurrentUser(), TimeProvider.System);

            return new Harness(db, bills, inventory, ledger);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Purchase.Repository.Clients;
using Purchase.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// TDS on a bill (TK-117). Accounting prices it; the bill credits TDS Payable
/// and owes the vendor the rest, and carries the quote to the ledger so the
/// register row is written with the legs.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class BillTdsTests
{
    private const long ItemId = 7;

    private readonly PostgresFixture _pg;

    public BillTdsTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public void The_vendor_is_owed_the_net_and_tds_payable_takes_the_deduction()
    {
        Bill bill = new()
        {
            BillId = 5,
            ContactId = 42,
            DocumentNo = "BIL-5",
            VendorBillNo = "INV-7",
            VendorBillDate = new DateOnly(2026, 6, 1),
            DocumentDate = new DateOnly(2026, 6, 1),
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            TdsSectionCode = "194C",
            TdsAmount = 1_050m,
            Lines =
            [
                new BillDetail
                {
                    BillDetailId = 1,
                    LineNumber = 1,
                    LineType = DocumentLineType.Expense,
                    AccountId = 61,
                    Quantity = 1m,
                    TaxableAmount = 30_000m,
                    TaxMasterId = 1,
                    Taxes =
                    [
                        new BillDetailTax { TaxComponent = TaxComponent.Cgst, Rate = 9m, TaxableAmount = 30_000m, Amount = 2_700m },
                        new BillDetailTax { TaxComponent = TaxComponent.Sgst, Rate = 9m, TaxableAmount = 30_000m, Amount = 2_700m },
                    ],
                },
            ],
        };

        List<LedgerLegRequest> legs = BillRepository.BuildLegs(bill, againstReceipt: false);

        LedgerLegRequest tds = Assert.Single(legs, l => l.AccountSystemName == BillRepository.TdsPayableAccount);
        Assert.Equal(1_050m, tds.CreditAmount);

        // A tax leg, not a control one: the bill's open balance is its control legs.
        Assert.Equal(2, tds.LedgerTypeId);

        // 30,000 plus 5,400 GST, less 1,050 TDS.
        Assert.Equal(34_350m, Assert.Single(legs, l => l.AccountSystemName == "Accounts Payable").CreditAmount);
        Assert.Equal(legs.Sum(l => l.DebitAmount), legs.Sum(l => l.CreditAmount));
    }

    [SkippableFact]
    public async Task Posting_a_bill_takes_accountings_quote_onto_the_bill_and_the_posting()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        StubTdsQuotes quotes = new(new TdsQuoteResult(TdsQuoteOutcome.Ok, new TdsQuote
        {
            Applies = true, Kind = 1, SectionCode = "194C", TdsBase = 105_000m, Rate = 1m, TdsAmount = 1_050m,
            Amount = 30_000m, Note = "This takes the year to 105,000.00, past the 100,000.00 threshold.",
        }, null));
        Harness h = await Harness.CreateAsync(_pg, quotes);

        BillResult created = await h.Bills.CreateAsync(Request(), default);
        BillResult posted = await h.Bills.PostAsync(created.BillId, default);
        Assert.Equal(BillOutcome.Ok, posted.Outcome);

        TdsQuoteRequest asked = Assert.Single(quotes.Requests);
        Assert.Equal(30_000m, asked.Amount);
        Assert.Equal("BIL", asked.TransactionTypeCode);
        Assert.Equal(created.BillId, asked.TransactionId);

        PostLedgerRequest sent = h.Ledger.Posts[^1];
        Assert.Equal(1_050m, sent.Tds!.TdsAmount);
        Assert.Equal(1_050m, sent.Legs.Where(l => l.AccountSystemName == BillRepository.TdsPayableAccount).Sum(l => l.CreditAmount));
        Assert.Equal(sent.Legs.Sum(l => l.DebitAmount), sent.Legs.Sum(l => l.CreditAmount));

        Bill bill = await h.Db.Bills.AsNoTracking().SingleAsync(b => b.BillId == created.BillId);
        Assert.Equal("194C", bill.TdsSectionCode);
        Assert.Equal(1_050m, bill.TdsAmount);
        Assert.Equal(105_000m, bill.TdsBase);
        Assert.Contains("past the 100,000.00 threshold", bill.TdsNote);
    }

    [SkippableFact]
    public async Task A_refused_quote_leaves_the_bill_a_draft_with_no_stock_moved()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg, new StubTdsQuotes(new TdsQuoteResult(
            TdsQuoteOutcome.Refused, null, "The contact's TDS section 194Z has no row in force.")));

        BillResult created = await h.Bills.CreateAsync(Request(), default);
        BillResult posted = await h.Bills.PostAsync(created.BillId, default);

        Assert.Equal(BillOutcome.PostingRefused, posted.Outcome);
        Assert.Contains("194Z", posted.Detail);
        Assert.Empty(h.Inventory.Calls);
        Assert.Empty(h.Ledger.Posts);
        Assert.Equal(DocumentStatus.Draft, (await h.Db.Bills.AsNoTracking().SingleAsync(b => b.BillId == created.BillId)).Status);
    }

    [SkippableFact]
    public async Task A_contact_without_tds_posts_the_whole_payable_and_still_tells_accounting()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        Harness h = await Harness.CreateAsync(_pg, new StubTdsQuotes(new TdsQuoteResult(
            TdsQuoteOutcome.Ok, new TdsQuote { Applies = false, Note = "TDS is off for this contact." }, null)));

        BillResult created = await h.Bills.CreateAsync(Request(), default);
        Assert.Equal(BillOutcome.Ok, (await h.Bills.PostAsync(created.BillId, default)).Outcome);

        PostLedgerRequest sent = h.Ledger.Posts[^1];
        Assert.False(sent.Tds!.Applies);
        Assert.DoesNotContain(sent.Legs, l => l.AccountSystemName == BillRepository.TdsPayableAccount);
    }

    private static SaveBillRequest Request() => new()
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
                Quantity = 300m,
                ConversionFactor = 1m,
                UnitPrice = 100m,
                TaxGroupId = 1,
                LineType = DocumentLineType.Stock,
            },
        ],
    };

    private sealed class StubTdsQuotes(TdsQuoteResult result) : ITdsQuoteClient
    {
        public List<TdsQuoteRequest> Requests { get; } = [];

        public Task<TdsQuoteResult> QuoteAsync(TdsQuoteRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    private sealed record Harness(PurchaseDbContext Db, BillRepository Bills, RecordingInventory Inventory, RecordingLedger Ledger)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg, ITdsQuoteClient quotes)
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
                new StubCurrentUser(), TimeProvider.System, tdsQuotes: quotes);

            return new Harness(db, bills, inventory, ledger);
        }
    }
}

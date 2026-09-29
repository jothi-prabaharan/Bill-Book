using Microsoft.EntityFrameworkCore;
using Purchase.Repository.Repositories;
using Purchase.Entity.Enums;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Shared.Kernel.Documents;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// GSTR-2B reconciliation (TK-116). The card's test: a recorded 2B sample
/// against seeded bills gives the expected matched, mismatched and missing
/// rows; and its "done when": every supplier invoice missing from the books
/// and every bill missing from 2B is listed.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class Gstr2bReconciliationTests
{
    private const string Acme = "29AABCU9603R1ZM";
    private const string Blue = "33AABCT1332L1ZZ";

    /// <summary>A June 2B as the portal downloads it, trimmed to four invoices.</summary>
    private const string Sample = """
        {
          "chksum": "x",
          "data": {
            "gstin": "33AAAAA0000A1Z5",
            "rtnprd": "062026",
            "gendt": "14-07-2026",
            "docdata": {
              "b2b": [
                {
                  "ctin": "29AABCU9603R1ZM",
                  "trdnm": "Acme Traders",
                  "inv": [
                    { "inum": "AC/0012", "dt": "05-06-2026", "val": 1180, "typ": "R", "pos": "33", "rev": "N", "itcavl": "Y",
                      "items": [ { "num": 1, "rt": 18, "txval": 1000, "igst": 180, "cgst": 0, "sgst": 0, "cess": 0 } ] },
                    { "inum": "AC-13", "dt": "09-06-2026", "val": 2360, "typ": "R", "pos": "33", "rev": "N", "itcavl": "Y",
                      "items": [ { "num": 1, "rt": 18, "txval": 2000, "igst": 360, "cgst": 0, "sgst": 0, "cess": 0 } ] },
                    { "inum": "AC-99", "dt": "20-06-2026", "val": 590, "typ": "R", "pos": "33", "rev": "N", "itcavl": "Y",
                      "items": [ { "num": 1, "rt": 18, "txval": 500, "igst": 90, "cgst": 0, "sgst": 0, "cess": 0 } ] }
                  ]
                },
                {
                  "ctin": "33AABCT1332L1ZZ",
                  "trdnm": "Blue Supplies",
                  "inv": [
                    { "inum": "B-1", "dt": "10-06-2026", "val": 1180, "typ": "R", "pos": "33", "rev": "N", "itcavl": "Y",
                      "items": [ { "num": 1, "rt": 18, "txval": 1000, "igst": 0, "cgst": 90, "sgst": 90, "cess": 0 } ] }
                  ]
                }
              ]
            }
          }
        }
        """;

    private readonly PostgresFixture _pg;

    public Gstr2bReconciliationTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public void A_recorded_2b_against_seeded_bills_gives_the_expected_groups()
    {
        Gstr2bDocument document = Gstr2bFileRepository.Parse(Sample);

        List<BooksBill> bills =
        [
            // AC/0012 recorded as "ac-12": a loose match, agreeing.
            new(1, "BIL-1", Acme, "ac-12", new DateOnly(2026, 6, 5), 1000m, 180m, 180m, false),
            // AC-13 recorded with 2,100 taxable: a mismatch.
            new(2, "BIL-2", Acme, "AC-13", new DateOnly(2026, 6, 9), 2100m, 378m, 378m, false),
            // B-1 exact and agreeing.
            new(3, "BIL-3", Blue, "B-1", new DateOnly(2026, 6, 10), 1000m, 180m, 180m, false),
            // A June bill Blue never reported.
            new(4, "BIL-4", Blue, "B-7", new DateOnly(2026, 6, 25), 400m, 72m, 72m, false),
        ];

        Gstr2bMatchResult result = Gstr2bMatcherRepository.Match(document.Invoices, bills, bills);

        Gstr2bLine loose = result.Lines.Single(l => l.InvoiceNo == "AC/0012");
        Assert.Equal(Gstr2bMatchState.Matched, loose.MatchState);
        Assert.Equal(1, loose.BillId);
        Assert.Contains("ignoring case, separators and leading zeros", loose.Note);

        Gstr2bLine mismatch = result.Lines.Single(l => l.InvoiceNo == "AC-13");
        Assert.Equal(Gstr2bMatchState.Mismatched, mismatch.MatchState);
        Assert.Contains("Taxable value differs by 100.00", mismatch.Note);

        Assert.Equal(Gstr2bMatchState.Matched, result.Lines.Single(l => l.InvoiceNo == "B-1").MatchState);

        // The card's "done when": both kinds of missing are listed.
        Gstr2bLine missingInBooks = Assert.Single(result.Lines, l => l.MatchState == Gstr2bMatchState.MissingInBooks);
        Assert.Equal("AC-99", missingInBooks.InvoiceNo);
        Gstr2bLine missingIn2b = Assert.Single(result.Lines, l => l.MatchState == Gstr2bMatchState.MissingIn2b);
        Assert.Equal("BIL-4", missingIn2b.BillNo);

        // At risk: the unreported bill's 72 and the 18 the mismatched bill claims above 2B.
        Assert.Equal(90m, result.ItcAtRisk);
    }

    [Fact]
    public void The_file_is_read_for_its_period_recipient_and_invoice_totals()
    {
        Gstr2bDocument document = Gstr2bFileRepository.Parse(Sample);

        Assert.Equal("33AAAAA0000A1Z5", document.RecipientGstin);
        Assert.Equal("062026", document.ReturnPeriod);
        Assert.Equal(new DateOnly(2026, 6, 1), document.From);
        Assert.Equal(new DateOnly(2026, 6, 30), document.To);
        Assert.Equal(4, document.Invoices.Count);

        Gstr2bInvoice blue = document.Invoices.Single(i => i.SupplierGstin == Blue);
        Assert.Equal(1000m, blue.Taxable);
        Assert.Equal(180m, blue.Tax);
        Assert.Equal("Blue Supplies", blue.SupplierName);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "data": { "docdata": { "b2b": [] } } }""")]
    [InlineData("""{ "data": { "rtnprd": "132026" } }""")]
    public void A_file_that_is_not_a_2b_is_refused_with_a_reason(string json) =>
        Assert.Throws<Gstr2bFormatException>(() => Gstr2bFileRepository.Parse(json));

    [Theory]
    [InlineData("INV/0007-A", "INV7A")]
    [InlineData("inv 7a", "INV7A")]
    [InlineData("000", "0")]
    [InlineData("2026-27/0045", "20262745")]
    public void An_invoice_number_is_normalised_to_letters_and_digits_without_leading_zeros(string number, string expected) =>
        Assert.Equal(expected, Gstr2bMatcherRepository.Normalise(number));

    [Fact]
    public void A_date_that_differs_is_a_mismatch_and_2b_withholding_credit_puts_it_at_risk()
    {
        List<Gstr2bInvoice> invoices =
        [
            new(Blue, null, "B-1", new DateOnly(2026, 6, 11), 1000m, 180m, false, ItcAvailable: false),
        ];
        List<BooksBill> bills = [new(3, "BIL-3", Blue, "B-1", new DateOnly(2026, 6, 10), 1000m, 180m, 180m, false)];

        Gstr2bLine line = Assert.Single(Gstr2bMatcherRepository.Match(invoices, bills, bills).Lines);

        Assert.Equal(Gstr2bMatchState.Mismatched, line.MatchState);
        Assert.Contains("Dated 11-06-2026 in 2B and 10-06-2026 in the books", line.Note);
        Assert.Contains("not available", line.Note);
        Assert.Equal(180m, Gstr2bMatcherRepository.Match(invoices, bills, bills).ItcAtRisk);
    }

    [SkippableFact]
    public async Task Importing_a_2b_stores_the_groups_against_the_purchase_register()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason);

        (Guid customer, Guid org) = (Guid.NewGuid(), Guid.NewGuid());
        await using PurchaseDbContext db = _pg.CreateContext(customer, org);

        db.PurchaseRegister.AddRange(
            Row(1, "BIL-1", Acme, "AC-12", new DateOnly(2026, 6, 5), 1000m, 180m),
            Row(3, "BIL-3", Blue, "B-1", new DateOnly(2026, 6, 10), 1000m, 180m),
            Row(4, "BIL-4", Blue, "B-7", new DateOnly(2026, 6, 25), 400m, 72m),
            Row(5, "BIL-5", Blue, "B-2", new DateOnly(2026, 5, 2), 100m, 18m));
        await db.SaveChangesAsync();

        Gstr2bRepository service = new(db);
        Gstr2bImportResult result = await service.ImportAsync("2b-june.json", Sample, default);

        Assert.Equal(Gstr2bImportOutcome.Ok, result.Outcome);

        Gstr2bImportDetail detail = (await service.GetAsync(result.Gstr2bImportId, default))!;
        Assert.Equal(2, detail.MatchedCount);
        Assert.Equal(2, detail.MissingInBooksCount);
        Assert.Equal(1, detail.MissingIn2bCount);
        Assert.Equal(72m, detail.ItcAtRisk);

        // A May bill is not "missing" from a June 2B.
        Assert.DoesNotContain(detail.Lines, l => l.BillNo == "BIL-5");
        Assert.Single(await service.ListAsync(default));
    }

    private static PurchaseRegister Row(long billId, string billNo, string gstin, string supplierNo, DateOnly date, decimal taxable, decimal tax) =>
        new()
        {
            TransactionTypeCode = "BIL",
            SourceId = billId,
            SourceLineId = billId * 10,
            DocumentNo = billNo,
            DocumentDate = date,
            SupplierInvoiceNo = supplierNo,
            SupplierInvoiceDate = date,
            ContactId = 1,
            ContactGstin = gstin,
            PlaceOfSupplyStateId = 31,
            ItcEligibility = ItcEligibility.Eligible,
            LineType = DocumentLineType.Stock,
            GstRate = 18m,
            Quantity = 1m,
            TaxableAmount = taxable,
            IgstAmount = tax,
            TotalAmount = taxable + tax,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            TaxableAmountBase = taxable,
            TaxAmountBase = tax,
        };
}

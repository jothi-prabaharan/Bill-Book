using System.Text.Json.Nodes;
using Reporting.Api.Services;
using Reporting.Repository.Repositories;
using Reporting.Repository.ReadModels;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// GSTR-3B from both registers and the GSTR-1 offline-tool export (TK-115),
/// over lists. The card's "done when" is the first test: a month with sales and
/// purchases shows both output tax and eligible input tax credit.
/// </summary>
public sealed class GstReturnTests
{
    private static readonly DateOnly June1 = new(2026, 6, 1);
    private static readonly DateOnly June30 = new(2026, 6, 30);

    private const int TamilNadu = 31;
    private const int Karnataka = 17;
    private const int Abroad = 99;

    private static readonly Dictionary<int, string> States = new()
    {
        [TamilNadu] = "33",
        [Karnataka] = "29",
        [Abroad] = "96",
    };

    private const string BranchGstin = "33AAACH7409R1Z8";

    /// <summary>The card's "done when".</summary>
    [Fact]
    public void Gstr3b_for_a_month_with_sales_and_purchases_shows_output_tax_and_eligible_itc()
    {
        List<SalesRegisterRead> sales =
        [
            Sale("INV-1", 1, taxable: 10_000m, cgst: 900m, sgst: 900m),
            Sale("INV-2", 2, taxable: 5_000m, igst: 900m, state: Karnataka, inter: true),
        ];
        List<PurchaseRegisterRead> purchases =
        [
            Purchase("BIL-1", 1, taxable: 4_000m, cgst: 360m, sgst: 360m),
            Purchase("BIL-2", 2, taxable: 1_000m, cgst: 90m, sgst: 90m, eligibility: "Blocked"),
        ];

        Gstr3bView view = Gstr3bRepository.Build(June1, June30, sales, purchases, States);

        Assert.Equal(15_000m, view.OutwardTaxable31a.Taxable);
        Assert.Equal(2_700m, view.OutwardTaxable31a.Tax);
        Assert.Equal(720m, view.ItcAllOther4A5.Tax);
        Assert.Equal(720m, view.NetItc4C.Tax);
        Assert.Equal(180m, view.IneligibleBlocked4D1.Tax);
        Assert.Equal(2_700m, view.TaxPayable.Tax);
        Assert.Equal(1_980m, view.NetPayable.Tax);
    }

    /// <summary>The card's test: 3B's ITC equals the register's eligible tax.</summary>
    [Fact]
    public void Gstr3b_itc_equals_the_registers_eligible_tax_less_debit_notes()
    {
        List<PurchaseRegisterRead> purchases =
        [
            Purchase("BIL-1", 1, taxable: 4_000m, cgst: 360m, sgst: 360m),
            Purchase("BIL-2", 2, taxable: 2_000m, igst: 360m, inter: true, reverseCharge: true),
            Purchase("BIL-3", 3, taxable: 1_000m, cgst: 90m, sgst: 90m, eligibility: "Ineligible"),
            Purchase("DBN-1", 1, taxable: 1_000m, cgst: 90m, sgst: 90m, type: "DBN"),
        ];

        Gstr3bView view = Gstr3bRepository.Build(June1, June30, [], purchases, States);

        decimal eligible = purchases
            .Where(p => p.ItcEligibility == "Eligible")
            .Sum(p => (p.TransactionTypeCode == "DBN" ? -1 : 1) * (p.CgstAmount + p.SgstAmount + p.IgstAmount + p.CessAmount));

        Assert.Equal(eligible, view.NetItc4C.Tax);
        Assert.Equal(360m, view.ItcReverseCharge4A3.Tax);
        Assert.Equal(540m, view.ItcAllOther4A5.Tax);
        Assert.Equal(180m, view.IneligibleOther4D2.Tax);

        // Reverse charge is a liability in 3.1(d) as well as a credit in 4A(3).
        Assert.Equal(2_000m, view.InwardReverseCharge31d.Taxable);
        Assert.Equal(360m, view.TaxPayable.Tax);
    }

    [Fact]
    public void Gstr3b_puts_exports_and_nil_rated_supplies_in_their_own_rows_and_counts_credit_notes_against_sales()
    {
        List<SalesRegisterRead> sales =
        [
            Sale("INV-1", 1, taxable: 10_000m, cgst: 900m, sgst: 900m),
            Sale("CRN-1", 1, taxable: -1_000m, cgst: -90m, sgst: -90m, type: "CRN"),
            Sale("INV-2", 2, taxable: 8_000m, state: Abroad, inter: true, rate: 0m),
            Sale("INV-3", 3, taxable: 500m, rate: 0m),
            Sale("DLC-1", 4, taxable: 99_999m, cgst: 9m, sgst: 9m, type: "DLC"),
            Sale("INV-9", 9, taxable: 7_777m, cgst: 1m, sgst: 1m, date: new DateOnly(2026, 7, 1)),
        ];

        Gstr3bView view = Gstr3bRepository.Build(June1, June30, sales, [], States);

        Assert.Equal(9_000m, view.OutwardTaxable31a.Taxable);
        Assert.Equal(1_620m, view.OutwardTaxable31a.Tax);
        Assert.Equal(8_000m, view.OutwardZeroRated31b.Taxable);
        Assert.Equal(500m, view.OutwardNilExempt31c.Taxable);
    }

    [Fact]
    public void Gstr3b_is_in_base_currency()
    {
        SalesRegisterRead sale = Sale("INV-1", 1, taxable: 100m, igst: 18m, inter: true, state: Karnataka);
        sale.ExchangeRate = 83m;
        sale.TaxableAmountBase = 8_300m;

        Gstr3bView view = Gstr3bRepository.Build(June1, June30, [sale], [], States);

        Assert.Equal(8_300m, view.OutwardTaxable31a.Taxable);
        Assert.Equal(1_494m, view.OutwardTaxable31a.Igst);
    }

    /// <summary>The card's test: the export's HSN summary equals its sections.</summary>
    [Fact]
    public void The_gstr1_exports_hsn_summary_equals_its_sections()
    {
        List<SalesRegisterRead> sales =
        [
            Sale("INV-1", 1, taxable: 10_000m, cgst: 900m, sgst: 900m, gstin: "29AAGCB7383J1Z4", hsn: "6109"),
            Sale("INV-1", 1, taxable: 2_000m, cgst: 60m, sgst: 60m, gstin: "29AAGCB7383J1Z4", hsn: "4901", rate: 6m),
            Sale("INV-2", 2, taxable: 150_000m, igst: 27_000m, state: Karnataka, inter: true, hsn: "8471"),
            Sale("INV-3", 3, taxable: 800m, cgst: 72m, sgst: 72m, hsn: "6109"),
            Sale("CRN-1", 1, taxable: -1_000m, cgst: -90m, sgst: -90m, type: "CRN", gstin: "29AAGCB7383J1Z4", hsn: "6109"),
            Sale("INV-4", 4, taxable: 3_000m, state: Abroad, inter: true, rate: 0m, hsn: "6109"),
        ];

        Gstr1ExportResult result = Gstr1ExportRepository.Build(BranchGstin, June1, June30, sales, States,
            [new IssuedDocuments(Gstr1ExportRepository.InvoicesNature, "INV-1", "INV-5", 5, 1)]);

        Assert.True(result.IsValid, string.Join("; ", result.Problems));
        Assert.Equal(result.SectionTaxable, result.HsnTaxable);
        Assert.Equal(sales.Sum(s => s.TaxableAmount), result.HsnTaxable);

        JsonObject json = result.Payload!;
        Assert.Equal("062026", json["fp"]!.GetValue<string>());

        decimal Sum(JsonArray? nodes, Func<JsonNode, decimal> f) => nodes?.Sum(n => f(n!)) ?? 0m;
        decimal Items(JsonNode invoice) => Sum(invoice["itms"]!.AsArray(), i => (i["itm_det"] ?? i)["txval"]!.GetValue<decimal>());

        decimal b2b = Sum(json["b2b"]!.AsArray(), p => Sum(p["inv"]!.AsArray(), Items));
        decimal b2cl = Sum(json["b2cl"]!.AsArray(), p => Sum(p["inv"]!.AsArray(), Items));
        decimal b2cs = Sum(json["b2cs"]!.AsArray(), r => r["txval"]!.GetValue<decimal>());
        decimal exp = Sum(json["exp"]!.AsArray(), p => Sum(p["inv"]!.AsArray(), Items));
        decimal cdnr = Sum(json["cdnr"]!.AsArray(), p => Sum(p["nt"]!.AsArray(), Items));
        decimal hsn = Sum(json["hsn"]!["data"]!.AsArray(), r => r["txval"]!.GetValue<decimal>());

        Assert.Equal(12_000m, b2b);
        Assert.Equal(150_000m, b2cl);
        Assert.Equal(800m, b2cs);
        Assert.Equal(3_000m, exp);
        Assert.Equal(1_000m, cdnr);
        Assert.Equal(b2b + b2cl + b2cs + exp - cdnr, hsn);

        // Intra-state rows carry the whole rate, not CGST's half.
        JsonNode first = json["b2b"]![0]!["inv"]![0]!["itms"]!.AsArray().Last()!;
        Assert.Equal(18m, first["itm_det"]!["rt"]!.GetValue<decimal>());

        JsonNode docs = json["doc_issue"]!["doc_det"]![0]!["docs"]![0]!;
        Assert.Equal(4, docs["net_issue"]!.GetValue<int>());
    }

    [Fact]
    public void An_export_is_refused_with_its_problems_listed()
    {
        List<SalesRegisterRead> sales =
        [
            Sale("INV-1", 1, taxable: 1_000m, cgst: 90m, sgst: 90m, gstin: "29NOTAGSTIN0000", hsn: "6109"),
            Sale("INV-2", 2, taxable: 1_000m, cgst: 90m, sgst: 90m, hsn: null),
        ];

        Gstr1ExportResult result = Gstr1ExportRepository.Build(null, June1, June30, sales, States, []);

        Assert.False(result.IsValid);
        Assert.Null(result.Payload);
        Assert.Contains(result.Problems, p => p.Contains("branch has no valid GSTIN"));
        Assert.Contains(result.Problems, p => p.Contains("INV-1") && p.Contains("not valid"));
        Assert.Contains(result.Problems, p => p.Contains("INV-2") && p.Contains("HSN"));
    }

    [Fact]
    public void An_inter_state_sale_to_an_unregistered_buyer_is_b2cl_only_above_the_threshold()
    {
        List<SalesRegisterRead> sales =
        [
            Sale("INV-1", 1, taxable: 80_000m, igst: 14_400m, state: Karnataka, inter: true, hsn: "8471"),
            Sale("INV-2", 2, taxable: 90_000m, igst: 16_200m, state: Karnataka, inter: true, hsn: "8471"),
        ];

        JsonObject json = Gstr1ExportRepository.Build(BranchGstin, June1, June30, sales, States, []).Payload!;

        // 94,400 is not above one lakh; 1,06,200 is.
        Assert.Equal("INV-2", json["b2cl"]![0]!["inv"]![0]!["inum"]!.GetValue<string>());
        Assert.Equal(80_000m, json["b2cs"]![0]!["txval"]!.GetValue<decimal>());
    }

    private static long _id;

    private static SalesRegisterRead Sale(
        string documentNo, long sourceId, decimal taxable,
        decimal cgst = 0m, decimal sgst = 0m, decimal igst = 0m,
        int state = TamilNadu, bool inter = false, string type = "INV",
        string? gstin = null, string? hsn = "6109", decimal? rate = null, DateOnly? date = null) =>
        new()
        {
            SalesRegisterId = Interlocked.Increment(ref _id),
            TransactionTypeCode = type,
            SourceId = sourceId,
            DocumentNo = documentNo,
            DocumentDate = date ?? new DateOnly(2026, 6, 10),
            ContactId = 1,
            ContactGstin = gstin,
            PlaceOfSupplyStateId = state,
            IsInterState = inter,
            SupplyType = gstin is null ? "B2CS" : "B2B",
            HsnSacCode = hsn,
            GstRate = rate ?? 18m,
            Quantity = type == "CRN" ? -1m : 1m,
            UqcCode = "NOS",
            TaxableAmount = taxable,
            CgstAmount = cgst,
            SgstAmount = sgst,
            IgstAmount = igst,
            TotalAmount = taxable + cgst + sgst + igst,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            TaxableAmountBase = taxable,
        };

    private static PurchaseRegisterRead Purchase(
        string documentNo, long sourceId, decimal taxable,
        decimal cgst = 0m, decimal sgst = 0m, decimal igst = 0m,
        bool inter = false, bool reverseCharge = false, string eligibility = "Eligible", string type = "BIL") =>
        new()
        {
            PurchaseRegisterId = Interlocked.Increment(ref _id),
            TransactionTypeCode = type,
            SourceId = sourceId,
            DocumentNo = documentNo,
            DocumentDate = new DateOnly(2026, 6, 12),
            SupplierInvoiceNo = $"S-{sourceId}",
            SupplierInvoiceDate = new DateOnly(2026, 6, 11),
            ContactId = 2,
            ContactGstin = "33AABCT1332L1ZZ",
            PlaceOfSupplyStateId = TamilNadu,
            IsInterState = inter,
            ReverseCharge = reverseCharge,
            ItcEligibility = eligibility,
            LineType = "Stock",
            HsnSacCode = "6109",
            GstRate = 18m,
            Quantity = 1m,
            TaxableAmount = taxable,
            CgstAmount = cgst,
            SgstAmount = sgst,
            IgstAmount = igst,
            TotalAmount = taxable + cgst + sgst + igst,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            TaxableAmountBase = taxable,
            TaxAmountBase = cgst + sgst + igst,
        };
}

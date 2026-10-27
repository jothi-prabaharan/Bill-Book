using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Payable Invoice Detail (TK-236), on books worked by hand. The branch's base currency is INR
/// and the period is October 2026. Two items, <b>Steel Rod</b> in Raw materials and <b>Bolt Box</b> in
/// Fasteners, and these bills, which are the Payable Invoice Summary's:
/// <list type="bullet">
/// <item><b>BIL-9501</b>: Steel Rod, 10 at 600 (taxable 6,000, tax 1,080 at 9% and 9%), and Bolt Box, 4 at
/// 1,000 (4,000, 720). 10,000 and 1,800: 11,800.</item>
/// <item><b>BIL-9502</b> (IGST, reverse charge): Bolt Box, 10 at 1,500 less a discount of 1,000 (14,000,
/// tax 2,520 at 18%), and a <b>Freight</b> line with no item, 1 at 6,000 (6,000, 1,080). 20,000 and
/// 3,600: 23,600.</item>
/// <item><b>BIL-9503</b>: Steel Rod, 5 at 1,000: 5,000, 900, 5,900; and a <b>CNC lathe</b> bought as a fixed
/// asset in Plant and machinery, 1 at 50,000 (50,000, 9,000 at 18%): 59,000. 55,000 and 9,900: 64,900.</item>
/// <item><b>BIL-9505</b> in US dollars at 80: Steel Rod, 5 at USD 100: taxable USD 500 and IGST USD 90,
/// USD 590, which is 47,200.</item>
/// </list>
/// For the lines of Steel Rod alone, 51,000 is taxable and 9,180 tax, 60,180 in all; for Fasteners,
/// 26,000, 4,680 and 30,680. The builders run on lists through the same expressions the repository runs
/// against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class PayableInvoiceDetailReportTests
{
    private const long Steelco = 9203, Boltworks = 9201;
    private const long SteelRod = 9611, BoltBox = 9612, RawMaterials = 9711, Fasteners = 9712;
    private const long Pcs = 40, PlantAndMachinery = 9801;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static ItemRead Item(long id, string code, string name, long category, bool active = true) =>
        new() { ItemId = id, ItemCode = code, ItemName = name, ItemCategoryId = category, IsActive = active };

    private static readonly List<ItemRead> Items =
    [
        Item(SteelRod, "STEEL-ROD", "Steel Rod", RawMaterials),
        Item(BoltBox, "BOLT-BOX", "Bolt Box", Fasteners),
    ];

    private static long _id;

    private static BillDetailRead Line(
        long bill, int number, long? item, string? description, decimal quantity, decimal price, decimal discount,
        decimal taxable, decimal tax, long? unit = Pcs, DocumentLineType type = DocumentLineType.Stock, long? assetCategory = null) =>
        new()
        {
            BillDetailId = ++_id, BillId = bill, LineNumber = number, ItemId = item, Description = description, UomId = unit,
            Quantity = quantity, UnitPrice = price, DiscountAmount = discount, GrossAmount = quantity * price, TaxableAmount = taxable,
            TaxAmount = tax, LineTotal = taxable + tax, LineType = type, FixedAssetCategoryId = assetCategory,
        };

    private static readonly List<BillDetailRead> Details =
    [
        Line(9501, 1, SteelRod, null, 10m, 600m, 0m, 6_000m, 1_080m),
        Line(9501, 2, BoltBox, null, 4m, 1_000m, 0m, 4_000m, 720m),
        Line(9502, 1, BoltBox, null, 10m, 1_500m, 1_000m, 14_000m, 2_520m),
        Line(9502, 2, null, "Freight", 1m, 6_000m, 0m, 6_000m, 1_080m, unit: null, type: DocumentLineType.Expense),
        Line(9503, 1, SteelRod, null, 5m, 1_000m, 0m, 5_000m, 900m),
        Line(9503, 2, null, "CNC lathe", 1m, 50_000m, 0m, 50_000m, 9_000m, unit: null, type: DocumentLineType.Capital, assetCategory: PlantAndMachinery),
        Line(9505, 1, SteelRod, null, 5m, 100m, 0m, 500m, 90m),
    ];

    private static PayableInvoiceRow BillRow(
        long id, DateOnly date, long contact, string currency, decimal rate, decimal totalInCurrency,
        PayableInvoiceStatus status = PayableInvoiceStatus.Paid, bool reverseCharge = false, string? vendorBillNo = null) =>
        new()
        {
            TypeCode = "BIL", DocumentId = id, DocumentNo = $"BIL-{id}", VendorBillNo = vendorBillNo ?? $"V/{id}", DocumentDate = date, DueDate = date.AddDays(30),
            ContactId = contact, CurrencyCode = currency, ExchangeRate = rate, TotalSource = totalInCurrency,
            Total = Math.Round(totalInCurrency * rate, 2), Status = status, ReverseCharge = reverseCharge,
        };

    private static readonly List<PayableInvoiceRow> Page =
    [
        BillRow(9505, new(2026, 10, 18), Boltworks, "USD", 80m, 590m, PayableInvoiceStatus.PartlyPaid),
        BillRow(9503, new(2026, 10, 10), Steelco, "INR", 1m, 64_900m, PayableInvoiceStatus.PartlyPaid),
        BillRow(9502, new(2026, 10, 5), Steelco, "INR", 1m, 23_600m, PayableInvoiceStatus.Overdue, reverseCharge: true),
        BillRow(9501, new(2026, 10, 2), Steelco, "INR", 1m, 11_800m),
    ];

    private static readonly List<BillLineTaxRate> TaxRates =
    [
        .. Details.Where(d => d.BillId is 9501 or 9503 && d.LineType == DocumentLineType.Stock && d.ItemId != null).SelectMany(d => new BillLineTaxRate[]
        {
            new() { BillDetailId = d.BillDetailId, Rate = 9m },
            new() { BillDetailId = d.BillDetailId, Rate = 9m },
        }),
        .. Details.Where(d => d.BillId is 9502 or 9505 || d.LineType == DocumentLineType.Capital).Select(d => new BillLineTaxRate { BillDetailId = d.BillDetailId, Rate = 18m }),
    ];

    private static readonly List<UnitRef> Units = [new() { UomId = Pcs, Code = "PCS" }];

    private static readonly List<AssetCategoryRef> AssetCategories = [new() { FixedAssetCategoryId = PlantAndMachinery, Name = "Plant and machinery" }];

    private static readonly List<VendorRef> Vendors =
    [
        new() { ContactId = Steelco, Code = "STEELCO", Name = "Steelco Metals" },
        new() { ContactId = Boltworks, Code = "BOLTWORKS", Name = "Boltworks Imports" },
    ];

    private static PayableInvoiceDetailReportRequest Request(
        long? item = null, long? category = null, PayableInvoiceStatus? status = null, long? contact = null) =>
        new() { FromDate = October1, ToDate = October31, ItemId = item, ItemCategoryId = category, Status = status, ContactId = contact };

    private static PayableInvoiceDetailReportResultDto Run(
        PayableInvoiceDetailReportRequest? request = null, IReadOnlyList<PayableInvoiceRow>? page = null,
        IReadOnlyList<BillDetailRead>? lines = null, IReadOnlyList<ItemRef>? items = null,
        IReadOnlyList<PayableStatusCount>? counts = null, PayableInvoiceDetailTotalsDto? totals = null)
    {
        request ??= Request();
        IReadOnlyList<PayableInvoiceRow> rows = page ?? Page;
        IReadOnlyList<BillDetailRead> shown = lines ?? [.. PayableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), request)];
        IReadOnlyList<ItemRef> names = items ?? [.. Items.Select(i => new ItemRef { ItemId = i.ItemId, Code = i.ItemCode, Name = i.ItemName })];

        return PayableInvoiceDetailReportRepository.Compose(
            request, rows, rows.Count, shown, TaxRates, names, Units, AssetCategories, Vendors, counts ?? [], totals ?? new(), "INR");
    }

    private static PayableInvoiceDetailBillDto Bill(PayableInvoiceDetailReportResultDto report, long id) =>
        report.Bills.Single(b => b.DocumentId == id);

    // ---- Which lines -------------------------------------------------------------------

    [Fact]
    public void With_no_item_or_category_every_line_is_a_line_of_the_report()
    {
        List<BillDetailRead> lines = [.. PayableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), Request())];

        Assert.Equal(7, lines.Count);
        Assert.False(PayableInvoiceDetailReportRepository.LinesAreFiltered(Request()));
    }

    [Fact]
    public void An_item_narrows_the_lines_to_those_for_it()
    {
        List<BillDetailRead> lines = [.. PayableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), Request(item: SteelRod))];

        Assert.Equal([9501L, 9503L, 9505L], lines.Select(d => d.BillId).Order());
        Assert.All(lines, d => Assert.Equal(SteelRod, d.ItemId));
        Assert.True(PayableInvoiceDetailReportRepository.LinesAreFiltered(Request(item: SteelRod)));
    }

    [Fact]
    public void A_category_narrows_the_lines_to_those_for_an_item_in_it_and_a_line_with_no_item_is_not_in_one()
    {
        List<BillDetailRead> lines = [.. PayableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), Request(category: Fasteners))];

        Assert.Equal(3, lines.Count);
        Assert.All(lines, d => Assert.Equal(BoltBox, d.ItemId));
        Assert.DoesNotContain(lines, d => d.ItemId == null);
    }

    [Fact]
    public void An_item_and_a_category_together_keep_only_the_lines_that_are_both()
    {
        Assert.Equal(3, PayableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), Request(item: BoltBox, category: Fasteners)).Count());
        Assert.Empty(PayableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), Request(item: BoltBox, category: RawMaterials)));
    }

    [Fact]
    public void The_request_the_summarys_builders_take_is_the_same_period_vendor_and_status_with_no_debit_notes()
    {
        var request = Request(status: PayableInvoiceStatus.Overdue, contact: Steelco);

        PayableInvoiceSummaryReportRequest summary = PayableInvoiceDetailReportRepository.SummaryRequestOf(request);

        Assert.Equal(
            (October1, October31, Steelco, PayableInvoiceStatus.Overdue, false, null),
            (summary.FromDate, summary.ToDate, summary.ContactId, summary.Status, summary.IncludeDebitNotes, summary.CurrencyCode));
    }

    // ---- The totals --------------------------------------------------------------------

    [Fact]
    public void A_line_is_taken_to_base_at_its_bills_rate()
    {
        List<BillLineFigure> figures = [.. PayableInvoiceDetailReportRepository.LineFigures(Details.AsQueryable(), Page.AsQueryable())];

        BillLineFigure usd = figures.Single(f => f.BillId == 9505);

        // USD 500, USD 90 and USD 590 at 80.
        Assert.Equal((40_000m, 7_200m, 47_200m), (usd.Taxable, usd.Tax, usd.Total));
    }

    [Fact]
    public void A_line_in_base_currency_is_as_it_is_and_a_rate_with_places_is_rounded_to_two()
    {
        PayableInvoiceRow[] rows = [BillRow(9502, October1, Steelco, "INR", 1m, 23_600m), BillRow(9505, October1, Boltworks, "USD", 83.4567m, 590m)];

        List<BillLineFigure> figures = [.. PayableInvoiceDetailReportRepository.LineFigures(Details.AsQueryable(), rows.AsQueryable())];

        Assert.Equal([14_000m, 6_000m], figures.Where(f => f.BillId == 9502).Select(f => f.Taxable));
        Assert.Equal(41_728.35m, figures.Single(f => f.BillId == 9505).Taxable);
    }

    [Fact]
    public void The_lines_of_the_matching_bills_add_up_to_the_summarys_taxable_value_tax_and_total()
    {
        List<BillLineFigure> figures = [.. PayableInvoiceDetailReportRepository.LineFigures(Details.AsQueryable(), Page.AsQueryable())];

        // 10,000 + 20,000 + 55,000 + 40,000, and 1,800 + 3,600 + 9,900 + 7,200, and the four bills' totals.
        Assert.Equal(125_000m, figures.Sum(f => f.Taxable));
        Assert.Equal(22_500m, figures.Sum(f => f.Tax));
        Assert.Equal(147_500m, figures.Sum(f => f.Total));
        Assert.Equal(Page.Sum(r => r.Total), figures.Sum(f => f.Total));
    }

    [Fact]
    public void The_lines_for_one_item_or_one_category_add_up_as_worked_by_hand()
    {
        var rod = Request(item: SteelRod);
        List<BillLineFigure> forRod = [.. PayableInvoiceDetailReportRepository.LineFigures(
            PayableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), rod), Page.AsQueryable())];
        Assert.Equal((51_000m, 9_180m, 60_180m), (forRod.Sum(f => f.Taxable), forRod.Sum(f => f.Tax), forRod.Sum(f => f.Total)));

        var fasteners = Request(category: Fasteners);
        List<BillLineFigure> forCategory = [.. PayableInvoiceDetailReportRepository.LineFigures(
            PayableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), fasteners), Page.AsQueryable())];
        Assert.Equal((26_000m, 4_680m, 30_680m), (forCategory.Sum(f => f.Taxable), forCategory.Sum(f => f.Tax), forCategory.Sum(f => f.Total)));
    }

    [Fact]
    public void A_line_of_a_bill_that_is_not_in_the_matching_set_is_not_counted()
    {
        PayableInvoiceRow[] only = [Page.Single(r => r.DocumentId == 9503)];

        Assert.Equal([9503L], PayableInvoiceDetailReportRepository.LineFigures(Details.AsQueryable(), only.AsQueryable()).Select(f => f.BillId).Distinct());
    }

    // ---- A bill and its lines ------------------------------------------------------------

    [Fact]
    public void A_bill_lists_its_lines_in_line_order_with_the_items_code_name_and_unit()
    {
        PayableInvoiceDetailBillDto bill = Bill(Run(), 9501);

        Assert.Equal([1, 2], bill.Lines.Select(l => l.LineNumber));
        PayableInvoiceDetailLineDto first = bill.Lines[0];
        Assert.Equal(("STEEL-ROD", "Steel Rod", "PCS"), (first.ItemCode, first.ItemName, first.UnitCode));
        Assert.Equal((10m, 600m, 6_000m, 1_080m, 7_080m), (first.Quantity, first.UnitPrice, first.Taxable, first.TaxAmount, first.LineTotal));
        Assert.False(first.IsCapital);
    }

    [Fact]
    public void A_line_with_no_item_reads_as_its_own_description_and_has_no_code_or_unit()
    {
        PayableInvoiceDetailLineDto freight = Bill(Run(), 9502).Lines.Single(l => l.LineNumber == 2);

        Assert.Equal((null, "Freight", null, null), (freight.ItemCode, freight.ItemName, freight.UnitCode, freight.Description));
        Assert.Null(freight.ItemId);
        Assert.False(freight.IsCapital);
    }

    [Fact]
    public void A_capital_line_is_marked_and_names_its_fixed_asset_category()
    {
        PayableInvoiceDetailLineDto lathe = Bill(Run(), 9503).Lines.Single(l => l.LineNumber == 2);

        Assert.True(lathe.IsCapital);
        Assert.Equal(("CNC lathe", "Plant and machinery"), (lathe.ItemName, lathe.AssetCategoryName));
        Assert.Equal(59_000m, lathe.LineTotal);
    }

    [Fact]
    public void A_capital_line_whose_category_the_names_do_not_know_is_still_marked()
    {
        BillDetailRead[] lines = [Line(9503, 1, null, "Old press", 1m, 100m, 0m, 100m, 18m, unit: null, type: DocumentLineType.Capital, assetCategory: 12345)];

        PayableInvoiceDetailLineDto line = Bill(Run(page: [Page[1]], lines: lines), 9503).Lines.Single();

        Assert.True(line.IsCapital);
        Assert.Null(line.AssetCategoryName);
    }

    [Fact]
    public void A_description_is_kept_only_when_it_says_more_than_the_items_name()
    {
        BillDetailRead[] lines =
        [
            Line(9501, 1, SteelRod, "Steel Rod", 1m, 100m, 0m, 100m, 18m),
            Line(9501, 2, BoltBox, "Bolt Box, galvanised", 1m, 100m, 0m, 100m, 18m),
        ];

        PayableInvoiceDetailBillDto bill = Bill(Run(page: [Page[3]], lines: lines), 9501);

        Assert.Null(bill.Lines[0].Description);
        Assert.Equal("Bolt Box, galvanised", bill.Lines[1].Description);
    }

    [Fact]
    public void A_line_for_an_item_the_names_do_not_know_falls_back_to_its_description()
    {
        BillDetailRead[] lines = [Line(9501, 1, 777, "Old item", 1m, 100m, 0m, 100m, 18m)];

        PayableInvoiceDetailLineDto line = Bill(Run(page: [Page[3]], lines: lines, items: []), 9501).Lines.Single();

        Assert.Equal((null, "Old item"), (line.ItemCode, line.ItemName));
    }

    [Fact]
    public void A_lines_tax_rate_is_the_rates_of_its_tax_rows_added_up()
    {
        PayableInvoiceDetailReportResultDto report = Run();

        // 9% and 9%, and a single 18% of IGST.
        Assert.Equal(18m, Bill(report, 9501).Lines[0].TaxRate);
        Assert.Equal(18m, Bill(report, 9502).Lines[0].TaxRate);
    }

    [Fact]
    public void A_line_with_no_tax_rows_has_a_tax_rate_of_nothing()
    {
        BillDetailRead[] lines = [Line(9501, 1, SteelRod, null, 1m, 100m, 0m, 100m, 0m)];

        Assert.Equal(0m, Bill(Run(page: [Page[3]], lines: lines), 9501).Lines.Single().TaxRate);
    }

    [Fact]
    public void A_discount_is_shown_on_its_line()
    {
        PayableInvoiceDetailLineDto discounted = Bill(Run(), 9502).Lines[0];

        Assert.Equal((1_000m, 14_000m), (discounted.DiscountAmount, discounted.Taxable));
    }

    [Fact]
    public void A_bill_adds_up_its_lines_in_its_own_currency()
    {
        PayableInvoiceDetailBillDto bill = Bill(Run(), 9502);

        Assert.Equal((20_000m, 3_600m, 23_600m), (bill.LinesTaxable, bill.LinesTax, bill.LinesTotal));
    }

    [Fact]
    public void A_bill_in_another_currency_carries_its_own_total_and_its_total_in_base()
    {
        PayableInvoiceDetailBillDto usd = Bill(Run(), 9505);

        Assert.Equal(("USD", 590m, 47_200m), (usd.CurrencyCode, usd.TotalInCurrency, usd.Total));
        Assert.Equal((500m, 90m, 590m), (usd.LinesTaxable, usd.LinesTax, usd.LinesTotal));
    }

    [Fact]
    public void With_every_line_shown_the_lines_and_the_round_off_make_the_total()
    {
        foreach (PayableInvoiceDetailBillDto bill in Run().Bills)
        {
            Assert.NotNull(bill.RoundOff);
            Assert.Equal(bill.TotalInCurrency, bill.LinesTotal + bill.RoundOff!.Value);
        }
    }

    [Fact]
    public void A_round_off_is_what_the_total_has_over_the_lines()
    {
        // 23,600 on the bill, 23,599.60 in its lines.
        BillDetailRead[] lines = [Line(9502, 1, BoltBox, null, 1m, 20_000m, 0m, 20_000m, 3_599.60m)];

        Assert.Equal(0.40m, Bill(Run(page: [Page[2]], lines: lines), 9502).RoundOff);
    }

    [Fact]
    public void When_an_item_or_category_narrows_the_lines_there_is_no_round_off_and_the_subtotals_are_of_the_lines_shown()
    {
        var request = Request(item: SteelRod);
        PayableInvoiceDetailReportResultDto report = Run(request, page: [Page[3]]);

        PayableInvoiceDetailBillDto bill = Bill(report, 9501);
        Assert.True(report.LinesFiltered);
        Assert.Null(bill.RoundOff);
        Assert.Equal([1], bill.Lines.Select(l => l.LineNumber));
        Assert.Equal((6_000m, 1_080m, 7_080m), (bill.LinesTaxable, bill.LinesTax, bill.LinesTotal));
        // The bill's own total is still the whole bill's.
        Assert.Equal(11_800m, bill.TotalInCurrency);
    }

    [Fact]
    public void A_bill_with_no_lines_is_still_listed_with_its_whole_total_as_round_off()
    {
        PayableInvoiceDetailBillDto bill = Bill(Run(page: [Page[3]], lines: []), 9501);

        Assert.Empty(bill.Lines);
        Assert.Equal((0m, 11_800m), (bill.LinesTotal, bill.RoundOff!.Value));
    }

    // ---- The page ----------------------------------------------------------------------

    [Fact]
    public void The_bills_are_in_the_order_given_with_their_vendors_status_dates_and_vendor_bill_numbers()
    {
        PayableInvoiceDetailReportResultDto report = Run();

        Assert.Equal([9505L, 9503L, 9502L, 9501L], report.Bills.Select(b => b.DocumentId));
        PayableInvoiceDetailBillDto overdue = Bill(report, 9502);
        Assert.Equal(("STEELCO", "Steelco Metals", PayableInvoiceStatus.Overdue), (overdue.ContactCode, overdue.ContactName, overdue.Status));
        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2026, 11, 4)), (overdue.DocumentDate, overdue.DueDate!.Value));
        Assert.Equal("V/9502", overdue.VendorBillNo);
    }

    [Fact]
    public void A_reverse_charge_bill_says_so()
    {
        PayableInvoiceDetailReportResultDto report = Run();

        Assert.True(Bill(report, 9502).ReverseCharge);
        Assert.False(Bill(report, 9501).ReverseCharge);
    }

    [Fact]
    public void A_vendor_the_names_do_not_know_is_still_listed_by_its_number()
    {
        PayableInvoiceRow[] page = [BillRow(9501, October1, 77, "INR", 1m, 10m)];

        PayableInvoiceDetailReportResultDto report = PayableInvoiceDetailReportRepository.Compose(Request(), page, 1, [], [], [], [], [], [], [], new(), "INR");

        Assert.Equal("Vendor 77", Assert.Single(report.Bills).ContactName);
    }

    [Fact]
    public void Every_status_has_a_count_a_nought_included()
    {
        PayableInvoiceDetailReportResultDto report = Run(counts: [new() { Status = PayableInvoiceStatus.Overdue, Count = 4 }]);

        Assert.Equal(Enum.GetValues<PayableInvoiceStatus>().Length, report.StatusCounts.Count);
        Assert.Equal(4, report.StatusCounts[PayableInvoiceStatus.Overdue]);
        Assert.Equal(0, report.StatusCounts[PayableInvoiceStatus.Paid]);
    }

    [Fact]
    public void The_period_the_page_and_the_totals_of_every_match_are_passed_on()
    {
        var totals = new PayableInvoiceDetailTotalsDto { Taxable = 1m, Tax = 2m, Total = 3m };
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        PayableInvoiceDetailReportResultDto report = Run(request, totals: totals);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 4), (report.PageNumber, report.PageSize, report.TotalBills));
        Assert.Same(totals, report.Totals);
    }

    // ---- Lookups -----------------------------------------------------------------------

    [Fact]
    public void The_items_offered_are_the_active_ones_by_name()
    {
        ItemRead[] items = [Item(1, "Z", "Zebra", RawMaterials), Item(2, "A", "Apple", RawMaterials), Item(3, "G", "Gone", RawMaterials, active: false)];

        Assert.Equal(["Apple", "Zebra"], PayableInvoiceDetailReportRepository.ItemChoices(items.AsQueryable()).Select(i => i.ItemName));
    }

    [Fact]
    public void The_categories_offered_are_the_active_ones_in_their_own_order()
    {
        ItemCategoryRead[] categories =
        [
            new() { ItemCategoryId = 1, CategoryCode = "B", CategoryName = "Second", DisplayOrder = 2, IsActive = true },
            new() { ItemCategoryId = 2, CategoryCode = "A", CategoryName = "First", DisplayOrder = 1, IsActive = true },
            new() { ItemCategoryId = 3, CategoryCode = "C", CategoryName = "Off", DisplayOrder = 0, IsActive = false },
        ];

        Assert.Equal(["First", "Second"], PayableInvoiceDetailReportRepository.CategoryChoices(categories.AsQueryable()).Select(c => c.CategoryName));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(PayableInvoiceDetailReportRequest.FromDate), nameof(PayableInvoiceDetailReportRequest.ToDate)],
            typeof(PayableInvoiceDetailReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new PayableInvoiceDetailReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new PayableInvoiceDetailReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date_and_an_unknown_status_at_its_field()
    {
        Assert.Equal(
            [(nameof(PayableInvoiceDetailReportRequest.ToDate), "To date cannot be before the from date.")],
            PayableInvoiceDetailReportRepository.Refusals(new PayableInvoiceDetailReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(PayableInvoiceDetailReportRequest.Status), "Status must be one of the statuses offered.")],
            PayableInvoiceDetailReportRepository.Refusals(Request(status: (PayableInvoiceStatus)9)));

        Assert.Empty(PayableInvoiceDetailReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new PayableInvoiceDetailReportController(null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("org_id", Guid.NewGuid().ToString())], "test")),
                },
            },
        };

        IActionResult result = await controller.Run(
            new PayableInvoiceDetailReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_purchase_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(PayableInvoiceDetailReportController);

        Assert.Equal("api/statements/payable-invoice-detail", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[]
        {
            nameof(PayableInvoiceDetailReportController.Run),
            nameof(PayableInvoiceDetailReportController.Vendors),
            nameof(PayableInvoiceDetailReportController.Items),
            nameof(PayableInvoiceDetailReportController.ItemCategories),
        })
        {
            Assert.Contains(
                "purchase.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }
    }

    [Fact]
    public void The_lookups_are_sub_routes_of_the_report()
    {
        Type controller = typeof(PayableInvoiceDetailReportController);

        Assert.Equal("vendors", controller.GetMethod(nameof(PayableInvoiceDetailReportController.Vendors))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("items", controller.GetMethod(nameof(PayableInvoiceDetailReportController.Items))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("item-categories", controller.GetMethod(nameof(PayableInvoiceDetailReportController.ItemCategories))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new PayableInvoiceDetailSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentNo", "documentDate", "contactName", "itemCode", "itemName", "quantity", "unitPrice", "lineTotal", "billId"],
            source.Columns.Select(c => c.Key));
    }
}

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
/// The Purchase Analysis (TK-239), on books worked by hand. The branch's base currency is INR and the period is
/// October 2026. Steel Rod is in Raw materials and Bolt Box in Fasteners. The lines:
/// <list type="bullet">
/// <item><b>BIL-9501</b> (Steelco): Steel Rod 10 at 600 (6,000) and Bolt Box 4 at 1,000 (4,000).</item>
/// <item><b>BIL-9502</b> (Steelco): Bolt Box 10 at 1,500 less a discount of 1,000 (gross 15,000, net 14,000) and a
/// <i>Freight</i> line with no item, 1 at 6,000.</item>
/// <item><b>BIL-9503</b> (Steelco): Steel Rod 5 at 1,000 (5,000).</item>
/// <item><b>BIL-9504</b> (Boltworks) in US dollars at 80: Steel Rod 5 at USD 100, which is 40,000 in base.</item>
/// <item><b>BIL-9505</b> (Boltworks): Bolt Box 8 at 1,000 (8,000) and a <i>CNC lathe</i> bought as a fixed asset, 1 at 50,000.</item>
/// <item>A voided bill, a draft, and a bill dated in September (Steel Rod 4 at 1,000), which are not in October.</item>
/// <item><b>DBN-9521</b> (a purchase return, Steelco): Bolt Box 1 at 1,000. <b>DBN-9522</b> (a price correction):
/// 2,000 off Bolt Box, with no units.</item>
/// </list>
/// By item, Steel Rod is 20 units, 51,000 (an average of 2,550); Bolt Box is 21 units (the return counts), gross 24,000,
/// discount 1,000, net 23,000; the line with no item is 1 unit and 6,000. Together 42 units, gross 81,000, net 80,000, with
/// the fixed asset left out (130,000 and 43 units with it). The builders run on lists through the same expressions the
/// repository runs against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class PurchaseAnalysisReportTests
{
    private const long Boltworks = 9112, Steelco = 9111;
    private const long SteelRod = 9611, BoltBox = 9612, RawMaterials = 9711, Fasteners = 9712;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static BillRead Bill(long id, DateOnly date, long contact, decimal rate = 1m, DocumentStatus status = DocumentStatus.Posted) =>
        new() { BillId = id, TransactionTypeCode = "BIL", DocumentNo = $"BIL-{id}", DocumentDate = date, ContactId = contact, ExchangeRate = rate, Status = status };

    private static readonly List<BillRead> Bills =
    [
        Bill(9501, new(2026, 10, 2), Steelco),
        Bill(9502, new(2026, 10, 5), Steelco),
        Bill(9503, new(2026, 10, 10), Steelco),
        Bill(9504, new(2026, 10, 18), Boltworks, rate: 80m),
        Bill(9505, new(2026, 10, 20), Boltworks),
        Bill(9506, new(2026, 10, 22), Steelco, status: DocumentStatus.Void),
        Bill(9507, new(2026, 10, 24), Steelco, status: DocumentStatus.Draft),
        Bill(9508, new(2026, 9, 20), Steelco),
    ];

    private static BillDetailRead Line(
        long id, long bill, long? item, decimal qty, decimal price, decimal discount, DocumentLineType type = DocumentLineType.Stock, int number = 1)
    {
        decimal gross = qty * price;
        return new()
        {
            BillDetailId = id, BillId = bill, LineNumber = number, ItemId = item, Quantity = qty, BaseQuantity = qty, UnitPrice = price, DiscountAmount = discount,
            GrossAmount = gross, TaxableAmount = gross - discount, LineType = type,
        };
    }

    private static readonly List<BillDetailRead> Details =
    [
        Line(9801, 9501, SteelRod, 10m, 600m, 0m),
        Line(9802, 9501, BoltBox, 4m, 1_000m, 0m, number: 2),
        Line(9803, 9502, BoltBox, 10m, 1_500m, 1_000m),
        Line(9804, 9502, null, 1m, 6_000m, 0m, DocumentLineType.Expense, number: 2),
        Line(9805, 9503, SteelRod, 5m, 1_000m, 0m),
        Line(9806, 9504, SteelRod, 5m, 100m, 0m),
        Line(9807, 9505, BoltBox, 8m, 1_000m, 0m),
        Line(9808, 9505, null, 1m, 50_000m, 0m, DocumentLineType.Capital, number: 2),
        Line(9809, 9506, SteelRod, 1m, 1_000m, 0m),
        Line(9810, 9507, BoltBox, 3m, 1_000m, 0m),
        Line(9811, 9508, SteelRod, 4m, 1_000m, 0m),
    ];

    private static DebitNoteRead Debit(long id, DateOnly date, DebitNoteReason reason) =>
        new() { DebitNoteId = id, BillId = 9502, TransactionTypeCode = "DBN", DocumentNo = $"DBN-{id}", DocumentDate = date, ContactId = Steelco, ExchangeRate = 1m, Status = DocumentStatus.Posted, ReasonCode = reason };

    private static readonly List<DebitNoteRead> DebitNotes =
    [
        Debit(9521, new(2026, 10, 25), DebitNoteReason.PurchaseReturn),
        Debit(9522, new(2026, 10, 28), DebitNoteReason.PriceCorrection),
    ];

    private static readonly List<DebitNoteDetailRead> DebitDetails =
    [
        new() { DebitNoteDetailId = 9901, DebitNoteId = 9521, BillDetailId = 9803, LineNumber = 1, ItemId = BoltBox, Quantity = 1m, BaseQuantity = 1m, GrossAmount = 1_000m, TaxableAmount = 1_000m, LineType = DocumentLineType.Stock },
        new() { DebitNoteDetailId = 9902, DebitNoteId = 9522, BillDetailId = 9803, LineNumber = 1, ItemId = BoltBox, Quantity = 2m, BaseQuantity = 2m, GrossAmount = 2_000m, TaxableAmount = 2_000m, LineType = DocumentLineType.Stock },
    ];

    private static ItemRead Item(long id, string code, string name, long category) =>
        new() { ItemId = id, ItemCode = code, ItemName = name, ItemCategoryId = category };

    private static readonly List<ItemRead> Items = [Item(SteelRod, "STEEL-ROD", "Steel Rod", RawMaterials), Item(BoltBox, "BOLT-BOX", "Bolt Box", Fasteners)];

    private static PurchaseAnalysisReportRequest Request(
        PurchaseAnalysisGroupBy by = PurchaseAnalysisGroupBy.Item, bool debits = true, bool assets = false, long? contact = null, long? category = null,
        DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, GroupBy = by, IncludeDebitNotes = debits, IncludeFixedAssets = assets, ContactId = contact, ItemCategoryId = category };

    private static List<PurchaseLine> LinesFor(PurchaseAnalysisReportRequest request) =>
    [
        .. PurchaseAnalysisReportRepository.Lines(
            Bills.AsQueryable(), Details.AsQueryable(), DebitNotes.AsQueryable(), DebitDetails.AsQueryable(), Items.AsQueryable(),
            request, request.FromDate!.Value, request.ToDate!.Value),
    ];

    private static List<PurchaseGroup> GroupsFor(PurchaseAnalysisReportRequest request) =>
        [.. PurchaseAnalysisReportRepository.Groups(LinesFor(request).AsQueryable(), request.GroupBy)];

    private static PurchaseGroup Of(IEnumerable<PurchaseGroup> groups, long key) => groups.Single(g => g.Key == key);

    // ---- Which lines ---------------------------------------------------------------------

    [Fact]
    public void Posted_bill_lines_in_the_period_are_purchases_and_a_void_a_draft_and_another_month_are_not()
    {
        List<PurchaseLine> lines = LinesFor(Request(debits: false));

        // Seven lines: 9801 to 9807, the fixed asset left out.
        Assert.Equal([9801L, 9802, 9803, 9804, 9805, 9806, 9807], lines.Select(l => l.LineKey).Order());
        Assert.Equal(80_000m + 3_000m, lines.Sum(l => l.Net));
    }

    [Fact]
    public void A_line_bought_as_a_fixed_asset_is_left_out_unless_asked_for()
    {
        Assert.DoesNotContain(LinesFor(Request(debits: false)), l => l.LineKey == 9808);

        List<PurchaseLine> with = LinesFor(Request(debits: false, assets: true));
        PurchaseLine lathe = with.Single(l => l.LineKey == 9808);
        Assert.Equal((1m, 50_000m, 0L), (lathe.Quantity, lathe.Net, lathe.ItemId));
        Assert.Equal(83_000m + 50_000m, with.Sum(l => l.Net));
    }

    [Fact]
    public void A_debit_notes_fixed_asset_line_is_left_out_with_the_asset_unless_asked_for()
    {
        DebitNoteDetailRead[] details = [.. DebitDetails, new() { DebitNoteDetailId = 9903, DebitNoteId = 9521, LineNumber = 2, Quantity = 1m, BaseQuantity = 1m, GrossAmount = 10_000m, TaxableAmount = 10_000m, LineType = DocumentLineType.Capital }];

        decimal Net(bool assets) => PurchaseAnalysisReportRepository.Lines(
            Bills.AsQueryable(), Details.AsQueryable(), DebitNotes.AsQueryable(), details.AsQueryable(), Items.AsQueryable(), Request(assets: assets), October1, October31).Sum(l => l.Net);

        Assert.Equal(80_000m, Net(false));
        Assert.Equal(80_000m + 50_000m - 10_000m, Net(true));
    }

    [Fact]
    public void Debit_note_lines_are_taken_off_unless_turned_off()
    {
        Assert.Equal(83_000m - 3_000m, LinesFor(Request()).Sum(l => l.Net));
        Assert.Equal(83_000m, LinesFor(Request(debits: false)).Sum(l => l.Net));
    }

    [Fact]
    public void A_line_in_another_currency_is_taken_to_base_at_the_documents_rate()
    {
        PurchaseLine usd = LinesFor(Request(debits: false)).Single(l => l.ContactId == Boltworks);

        // USD 500 at 80.
        Assert.Equal((40_000m, 0m, 40_000m), (usd.Gross, usd.Discount, usd.Net));
    }

    [Fact]
    public void Net_purchases_are_after_discount_and_gross_is_before_it()
    {
        PurchaseLine discounted = LinesFor(Request()).Single(l => l.Discount == 1_000m);

        Assert.Equal((15_000m, 1_000m, 14_000m), (discounted.Gross, discounted.Discount, discounted.Net));
    }

    [Fact]
    public void A_return_gives_units_and_value_back_and_a_price_correction_only_value()
    {
        List<PurchaseLine> credits = [.. LinesFor(Request()).Where(l => l.Net < 0m).OrderBy(l => l.Net)];

        // The price correction first (-2,000), then the return (-1,000).
        Assert.Equal((0m, -2_000m), (credits[0].Quantity, credits[0].Net));
        Assert.Equal((-1m, -1_000m), (credits[1].Quantity, credits[1].Net));
        Assert.Equal(["DBN", "DBN"], credits.Select(c => c.TypeCode));
    }

    [Fact]
    public void The_vendor_and_category_each_narrow_the_lines()
    {
        Assert.Equal(48_000m, LinesFor(Request(contact: Boltworks)).Sum(l => l.Net));
        Assert.Equal(23_000m, LinesFor(Request(category: Fasteners)).Sum(l => l.Net));
        Assert.Equal(51_000m, LinesFor(Request(category: RawMaterials)).Sum(l => l.Net));
    }

    // ---- Groups ---------------------------------------------------------------------------

    [Fact]
    public void By_item_each_item_adds_up_its_lines_and_returns_and_lines_with_no_item_are_one_group()
    {
        List<PurchaseGroup> groups = GroupsFor(Request());

        Assert.Equal([0L, SteelRod, BoltBox], groups.Select(g => g.Key).Order());
        PurchaseGroup rod = Of(groups, SteelRod);
        Assert.Equal((20m, 51_000m, 0m, 51_000m), (rod.Quantity, rod.Gross, rod.Discount, rod.Net));
        PurchaseGroup box = Of(groups, BoltBox);
        Assert.Equal((21m, 24_000m, 1_000m, 23_000m), (box.Quantity, box.Gross, box.Discount, box.Net));
        PurchaseGroup none = Of(groups, 0);
        Assert.Equal((1m, 6_000m, 6_000m), (none.Quantity, none.Gross, none.Net));
    }

    [Fact]
    public void By_category_the_items_of_a_category_add_up_and_a_line_with_no_item_has_no_category()
    {
        List<PurchaseGroup> groups = GroupsFor(Request(PurchaseAnalysisGroupBy.Category));

        Assert.Equal([0L, RawMaterials, Fasteners], groups.Select(g => g.Key).Order());
        Assert.Equal(51_000m, Of(groups, RawMaterials).Net);
        Assert.Equal(23_000m, Of(groups, Fasteners).Net);
        Assert.Equal(6_000m, Of(groups, 0).Net);
    }

    [Fact]
    public void By_vendor_each_vendor_adds_up_everything_bought_from_them_less_what_went_back()
    {
        List<PurchaseGroup> groups = GroupsFor(Request(PurchaseAnalysisGroupBy.Vendor));

        // Steelco: 10,000 + 20,000 + 5,000 less 1,000 and 2,000. Boltworks: 40,000 + 8,000.
        Assert.Equal(32_000m, Of(groups, Steelco).Net);
        Assert.Equal(48_000m, Of(groups, Boltworks).Net);
    }

    [Fact]
    public void However_it_is_grouped_the_groups_add_up_to_the_same_total()
    {
        foreach (PurchaseAnalysisGroupBy by in Enum.GetValues<PurchaseAnalysisGroupBy>())
        {
            List<PurchaseGroup> groups = GroupsFor(Request(by));

            Assert.Equal(80_000m, groups.Sum(g => g.Net));
            Assert.Equal(42m, groups.Sum(g => g.Quantity));
        }
    }

    [Fact]
    public void The_total_adds_up_every_line()
    {
        PurchaseGroup total = PurchaseAnalysisReportRepository.Total(LinesFor(Request()).AsQueryable()).Single();

        Assert.Equal((42m, 81_000m, 1_000m, 80_000m), (total.Quantity, total.Gross, total.Discount, total.Net));
    }

    [Fact]
    public void With_fixed_assets_asked_for_the_total_includes_them()
    {
        PurchaseGroup total = PurchaseAnalysisReportRepository.Total(LinesFor(Request(assets: true)).AsQueryable()).Single();

        Assert.Equal((43m, 131_000m, 130_000m), (total.Quantity, total.Gross, total.Net));
    }

    // ---- Average cost, share and change -----------------------------------------------------

    [Fact]
    public void An_average_cost_is_net_purchases_over_units_and_only_when_adding_up_by_item()
    {
        List<PurchaseGroup> groups = GroupsFor(Request());

        Assert.Equal(2_550m, PurchaseAnalysisReportRepository.AverageCostOf(Of(groups, SteelRod), PurchaseAnalysisGroupBy.Item));
        Assert.Equal(1_095.238095m, PurchaseAnalysisReportRepository.AverageCostOf(Of(groups, BoltBox), PurchaseAnalysisGroupBy.Item));
        Assert.Null(PurchaseAnalysisReportRepository.AverageCostOf(Of(groups, SteelRod), PurchaseAnalysisGroupBy.Category));
        Assert.Null(PurchaseAnalysisReportRepository.AverageCostOf(Of(groups, SteelRod), PurchaseAnalysisGroupBy.Vendor));
    }

    [Fact]
    public void The_group_of_lines_with_no_item_has_no_average_cost_since_a_unit_means_nothing_there()
    {
        Assert.Null(PurchaseAnalysisReportRepository.AverageCostOf(Of(GroupsFor(Request()), 0), PurchaseAnalysisGroupBy.Item));
    }

    [Fact]
    public void A_group_with_no_units_has_no_average_cost()
    {
        Assert.Null(PurchaseAnalysisReportRepository.AverageCostOf(new PurchaseGroup { Quantity = 0m, Net = -2_000m }, PurchaseAnalysisGroupBy.Item));
    }

    [Fact]
    public void A_share_is_a_groups_part_of_all_net_purchases_and_nothing_when_there_are_none()
    {
        Assert.Equal(63.75m, PurchaseAnalysisReportRepository.ShareOf(51_000m, 80_000m));
        Assert.Equal(7.5m, PurchaseAnalysisReportRepository.ShareOf(6_000m, 80_000m));
        Assert.Null(PurchaseAnalysisReportRepository.ShareOf(100m, 0m));
    }

    [Fact]
    public void Net_purchases_change_is_a_share_of_the_comparison_and_nothing_when_there_is_no_comparison_or_nothing_was_bought_in_it()
    {
        Assert.Equal(1_175m, PurchaseAnalysisReportRepository.ChangeOf(51_000m, 4_000m));
        Assert.Equal(-50m, PurchaseAnalysisReportRepository.ChangeOf(50m, 100m));
        Assert.Null(PurchaseAnalysisReportRepository.ChangeOf(51_000m, 0m));
        Assert.Null(PurchaseAnalysisReportRepository.ChangeOf(51_000m, null));
    }

    // ---- Compose --------------------------------------------------------------------------

    private static readonly List<LabelRef> Labels =
    [
        new() { Key = SteelRod, Code = "STEEL-ROD", Name = "Steel Rod" },
        new() { Key = BoltBox, Code = "BOLT-BOX", Name = "Bolt Box" },
    ];

    private static PurchaseAnalysisReportResultDto Run(PurchaseAnalysisReportRequest? request = null, bool compare = false)
    {
        request ??= Request();
        List<PurchaseGroup> groups = [.. GroupsFor(request).OrderByDescending(g => g.Net).ThenBy(g => g.Key)];
        PurchaseGroup total = PurchaseAnalysisReportRepository.Total(LinesFor(request).AsQueryable()).Single();
        List<PurchaseGroup> earlier = compare ? [new PurchaseGroup { Key = SteelRod, Net = 4_000m }] : [];

        return PurchaseAnalysisReportRepository.Compose(request, groups, groups.Count, Labels, earlier, total, compare ? 4_000m : null, "INR");
    }

    [Fact]
    public void The_groups_are_largest_net_purchases_first_with_their_names_average_costs_and_shares()
    {
        PurchaseAnalysisReportResultDto report = Run();

        Assert.Equal([SteelRod, BoltBox, 0L], report.Groups.Select(g => g.Key));
        PurchaseAnalysisGroupDto box = report.Groups[1];
        Assert.Equal(("BOLT-BOX", "Bolt Box", 1_095.238095m, 28.75m), (box.Code, box.Name, box.AverageCost, box.SharePercent));
        Assert.Equal(63.75m, report.Groups[0].SharePercent);
        Assert.Equal(7.5m, report.Groups[2].SharePercent);
    }

    [Fact]
    public void The_shares_of_all_the_groups_make_the_whole()
    {
        Assert.Equal(100m, Run().Groups.Sum(g => g.SharePercent));
    }

    [Fact]
    public void The_group_of_lines_with_no_item_category_or_vendor_is_named_for_that()
    {
        Assert.Equal("(No item)", Run().Groups.Single(g => g.Key == 0).Name);
        Assert.Equal("(No category)", Run(Request(PurchaseAnalysisGroupBy.Category)).Groups.Single(g => g.Key == 0).Name);
        Assert.Equal("(No vendor)", PurchaseAnalysisReportRepository.NoneLabel(PurchaseAnalysisGroupBy.Vendor));
    }

    [Fact]
    public void Adding_up_by_category_or_vendor_gives_no_average_cost_to_a_group()
    {
        Assert.All(Run(Request(PurchaseAnalysisGroupBy.Category)).Groups, g => Assert.Null(g.AverageCost));
        Assert.All(Run(Request(PurchaseAnalysisGroupBy.Vendor)).Groups, g => Assert.Null(g.AverageCost));
    }

    [Fact]
    public void An_item_group_has_an_average_cost_and_the_line_with_no_item_has_none()
    {
        PurchaseAnalysisReportResultDto report = Run();

        Assert.Equal(2_550m, report.Groups.Single(g => g.Key == SteelRod).AverageCost);
        Assert.Null(report.Groups.Single(g => g.Key == 0).AverageCost);
    }

    [Fact]
    public void The_totals_carry_the_whole()
    {
        PurchaseAnalysisTotalsDto totals = Run().Totals;

        Assert.Equal((42m, 81_000m, 1_000m, 80_000m), (totals.Quantity, totals.Gross, totals.Discount, totals.Net));
    }

    [Fact]
    public void A_comparison_period_gives_each_group_its_net_purchases_then_and_how_they_changed_and_a_group_with_none_then_has_none()
    {
        PurchaseAnalysisReportResultDto report = Run(compare: true);

        PurchaseAnalysisGroupDto rod = report.Groups.Single(g => g.Key == SteelRod);
        Assert.Equal((4_000m, 1_175m), (rod.CompareNet, rod.ChangePercent));
        PurchaseAnalysisGroupDto box = report.Groups.Single(g => g.Key == BoltBox);
        Assert.Equal((0m, null), (box.CompareNet, box.ChangePercent));
        Assert.Equal((4_000m, 1_900m), (report.Totals.CompareNet, report.Totals.ChangePercent));
    }

    [Fact]
    public void Without_a_comparison_period_nothing_is_compared()
    {
        PurchaseAnalysisReportResultDto report = Run();

        Assert.All(report.Groups, g => Assert.Equal((null, null), (g.CompareNet, g.ChangePercent)));
        Assert.Equal((null, null), (report.Totals.CompareNet, report.Totals.ChangePercent));
    }

    [Fact]
    public void The_period_the_grouping_debit_notes_fixed_assets_and_page_are_passed_on()
    {
        var request = Request(PurchaseAnalysisGroupBy.Vendor, debits: false, assets: true);
        request.PageNumber = 2;
        request.PageSize = 10;

        PurchaseAnalysisReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR", PurchaseAnalysisGroupBy.Vendor, false, true), (report.FromDate, report.ToDate, report.BaseCurrencyCode, report.GroupBy, report.IncludesDebitNotes, report.IncludesFixedAssets));
        Assert.Equal((2, 10, 2), (report.PageNumber, report.PageSize, report.TotalGroups));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(PurchaseAnalysisReportRequest.FromDate), nameof(PurchaseAnalysisReportRequest.ToDate)],
            typeof(PurchaseAnalysisReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new PurchaseAnalysisReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new PurchaseAnalysisReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void By_default_it_groups_by_item_with_debit_notes_on_and_fixed_assets_off()
    {
        var request = new PurchaseAnalysisReportRequest();

        Assert.Equal((PurchaseAnalysisGroupBy.Item, true, false), (request.GroupBy, request.IncludeDebitNotes, request.IncludeFixedAssets));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_an_unknown_grouping_and_half_a_comparison_are_refused_at_their_fields()
    {
        Assert.Equal(
            [(nameof(PurchaseAnalysisReportRequest.ToDate), "To date cannot be before the from date.")],
            PurchaseAnalysisReportRepository.Refusals(Request(from: October31, to: October1)));

        Assert.Equal(
            [(nameof(PurchaseAnalysisReportRequest.GroupBy), "Group by must be one of the options offered.")],
            PurchaseAnalysisReportRepository.Refusals(Request((PurchaseAnalysisGroupBy)9)));

        var halfFrom = Request();
        halfFrom.CompareFromDate = new DateOnly(2026, 9, 1);
        Assert.Equal(
            [(nameof(PurchaseAnalysisReportRequest.CompareToDate), "Compare to date is required when a compare from date is given.")],
            PurchaseAnalysisReportRepository.Refusals(halfFrom));

        var halfTo = Request();
        halfTo.CompareToDate = new DateOnly(2026, 9, 30);
        Assert.Equal(
            [(nameof(PurchaseAnalysisReportRequest.CompareFromDate), "Compare from date is required when a compare to date is given.")],
            PurchaseAnalysisReportRepository.Refusals(halfTo));

        var reversed = Request();
        reversed.CompareFromDate = new DateOnly(2026, 9, 30);
        reversed.CompareToDate = new DateOnly(2026, 9, 1);
        Assert.Equal(
            [(nameof(PurchaseAnalysisReportRequest.CompareToDate), "Compare to date cannot be before the compare from date.")],
            PurchaseAnalysisReportRepository.Refusals(reversed));

        Assert.Empty(PurchaseAnalysisReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new PurchaseAnalysisReportController(null!)
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
            new PurchaseAnalysisReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_purchase_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(PurchaseAnalysisReportController);

        Assert.Equal("api/statements/purchase-analysis", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[]
        {
            nameof(PurchaseAnalysisReportController.Run),
            nameof(PurchaseAnalysisReportController.Vendors),
            nameof(PurchaseAnalysisReportController.ItemCategories),
        })
        {
            Assert.Contains(
                "purchase.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("vendors", controller.GetMethod(nameof(PurchaseAnalysisReportController.Vendors))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("item-categories", controller.GetMethod(nameof(PurchaseAnalysisReportController.ItemCategories))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new PurchaseAnalysisSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(["itemCode", "itemName", "category", "quantity", "grossAmount", "itemId"], source.Columns.Select(c => c.Key));
    }
}

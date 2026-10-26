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
/// The Sales Analysis (TK-228), on books worked by hand. The branch's base currency is INR and the period is
/// October 2026. Widget A is in Hardware and Gadget B in Electronics. The lines, with the cost of the stock
/// movement each caused:
/// <list type="bullet">
/// <item><b>INV-9401</b> (Zenith): Widget A 10 at 600 (6,000, cost 3,000) and Gadget B 4 at 1,000 (4,000, cost 2,000).</item>
/// <item><b>INV-9402</b> (Zenith): Gadget B 10 at 1,500 less a discount of 1,000 (gross 15,000, net 14,000, cost 5,000) and an
/// <i>Installation</i> line with no item, 6,000 and no stock.</item>
/// <item><b>INV-9403</b> (Zenith): Widget A 5 at 1,000 (5,000), whose stock movement has not been costed yet.</item>
/// <item><b>INV-9404</b> (Zenith): Gadget B 8 at 1,000 (8,000, cost 4,000).</item>
/// <item><b>INV-9405</b> (Acme) in US dollars at 80: Widget A 5 at USD 100, which is 40,000 in base (cost 1,500).</item>
/// <item>A voided invoice, a draft, and an invoice dated in September (Widget A 4 at 1,000), which are not in October.</item>
/// <item><b>CRN-9421</b> (a sales return, Zenith): Gadget B 1 at 1,000, which also puts 500 of cost back.
/// <b>CRN-9422</b> (a price correction): 2,000 off Gadget B, with no units and no cost.</item>
/// </list>
/// By item, Widget A is 20 units, 51,000 and cost 4,500 with a line pending; Gadget B is 21 units (the return counts),
/// gross 24,000, discount 1,000, net 23,000, cost 10,500, margin 12,500 (54.35%); the line with no item is 1 unit and
/// 6,000. Together 42 units, gross 81,000, net 80,000. The builders run on lists through the same expressions the
/// repository runs against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class SalesAnalysisReportTests
{
    internal const long Acme = 9101, Zenith = 9103;
    internal const long WidgetA = 9601, GadgetB = 9602, Hardware = 9701, Electronics = 9702, Main = 9201;

    internal static readonly DateOnly October1 = new(2026, 10, 1);
    internal static readonly DateOnly October31 = new(2026, 10, 31);

    private static InvoiceRead Invoice(long id, DateOnly date, long contact, decimal rate = 1m, DocumentStatus status = DocumentStatus.Posted) =>
        new() { InvoiceId = id, TransactionTypeCode = "INV", DocumentNo = $"INV-{id}", DocumentDate = date, ContactId = contact, ExchangeRate = rate, Status = status };

    internal static readonly List<InvoiceRead> Invoices =
    [
        Invoice(9401, new(2026, 10, 2), Zenith),
        Invoice(9402, new(2026, 10, 5), Zenith),
        Invoice(9403, new(2026, 10, 10), Zenith),
        Invoice(9404, new(2026, 10, 15), Zenith),
        Invoice(9405, new(2026, 10, 18), Acme, rate: 80m),
        Invoice(9406, new(2026, 10, 20), Zenith, status: DocumentStatus.Void),
        Invoice(9407, new(2026, 10, 22), Zenith, status: DocumentStatus.Draft),
        Invoice(9408, new(2026, 9, 20), Zenith),
    ];

    private static InvoiceDetailRead Line(long id, long invoice, long? item, decimal qty, decimal price, decimal discount, long? warehouse = null)
    {
        decimal gross = qty * price;
        return new()
        {
            InvoiceDetailId = id, InvoiceId = invoice, LineNumber = id == 9804 ? 2 : id == 9802 ? 2 : 1, ItemId = item, Quantity = qty, BaseQuantity = qty, UnitPrice = price, DiscountAmount = discount,
            GrossAmount = gross, TaxableAmount = gross - discount, WarehouseId = warehouse,
        };
    }

    internal static readonly List<InvoiceDetailRead> Details =
    [
        Line(9801, 9401, WidgetA, 10m, 600m, 0m, Main),
        Line(9802, 9401, GadgetB, 4m, 1_000m, 0m),
        Line(9803, 9402, GadgetB, 10m, 1_500m, 1_000m),
        Line(9804, 9402, null, 1m, 6_000m, 0m),
        Line(9805, 9403, WidgetA, 5m, 1_000m, 0m, Main),
        Line(9806, 9404, GadgetB, 8m, 1_000m, 0m),
        Line(9807, 9405, WidgetA, 5m, 100m, 0m, Main),
        Line(9808, 9406, WidgetA, 1m, 1_000m, 0m, Main),
        Line(9809, 9407, GadgetB, 3m, 1_000m, 0m),
        Line(9810, 9408, WidgetA, 4m, 1_000m, 0m),
    ];

    private static CreditNoteRead Credit(long id, DateOnly date, CreditNoteReason reason) =>
        new() { CreditNoteId = id, TransactionTypeCode = "CRN", DocumentNo = $"CRN-{id}", DocumentDate = date, ContactId = Zenith, ExchangeRate = 1m, Status = DocumentStatus.Posted, ReasonCode = reason };

    internal static readonly List<CreditNoteRead> CreditNotes =
    [
        Credit(9421, new(2026, 10, 25), CreditNoteReason.SalesReturn),
        Credit(9422, new(2026, 10, 28), CreditNoteReason.PriceCorrection),
    ];

    internal static readonly List<CreditNoteDetailRead> CreditDetails =
    [
        new() { CreditNoteDetailId = 9901, CreditNoteId = 9421, LineNumber = 1, ItemId = GadgetB, Quantity = 1m, BaseQuantity = 1m, UnitPrice = 1_000m, GrossAmount = 1_000m, TaxableAmount = 1_000m },
        new() { CreditNoteDetailId = 9902, CreditNoteId = 9422, LineNumber = 1, ItemId = GadgetB, Quantity = 2m, BaseQuantity = 2m, UnitPrice = 1_000m, GrossAmount = 2_000m, TaxableAmount = 2_000m },
    ];

    private static ItemRead Item(long id, string code, string name, long category) =>
        new() { ItemId = id, ItemCode = code, ItemName = name, ItemCategoryId = category };

    internal static readonly List<ItemRead> Items = [Item(WidgetA, "WIDGET-A", "Widget A", Hardware), Item(GadgetB, "GADGET-B", "Gadget B", Electronics)];

    private static StockMovementRead Movement(string type, long source, long line, decimal? cost, CostingStatus status = CostingStatus.Costed) =>
        new() { SourceType = type, SourceId = source, SourceLineId = line, TotalCost = cost, CostingStatus = status };

    internal static readonly List<StockMovementRead> Movements =
    [
        Movement("INV", 9401, 9801, 3_000m),
        Movement("INV", 9401, 9802, 2_000m),
        Movement("INV", 9402, 9803, 5_000m),
        Movement("INV", 9403, 9805, null, CostingStatus.Pending),
        Movement("INV", 9404, 9806, 4_000m),
        Movement("INV", 9405, 9807, 1_500m),
        Movement("CRN", 9421, 9901, 500m),
    ];

    private static SalesAnalysisReportRequest Request(
        SalesAnalysisGroupBy by = SalesAnalysisGroupBy.Item, bool credits = true, long? contact = null, long? category = null, long? warehouse = null,
        DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, GroupBy = by, IncludeCreditNotes = credits, ContactId = contact, ItemCategoryId = category, WarehouseId = warehouse };

    private static List<AnalysisLine> LinesFor(SalesAnalysisReportRequest request) =>
    [
        .. SalesAnalysisReportRepository.Lines(
            Invoices.AsQueryable(), Details.AsQueryable(), CreditNotes.AsQueryable(), CreditDetails.AsQueryable(), Items.AsQueryable(), Movements.AsQueryable(),
            request, request.FromDate!.Value, request.ToDate!.Value),
    ];

    private static List<AnalysisGroup> GroupsFor(SalesAnalysisReportRequest request) =>
        [.. SalesAnalysisReportRepository.Groups(LinesFor(request).AsQueryable(), request.GroupBy)];

    private static AnalysisGroup Of(IEnumerable<AnalysisGroup> groups, long key) => groups.Single(g => g.Key == key);

    // ---- Which lines ---------------------------------------------------------------------

    [Fact]
    public void Posted_invoice_lines_in_the_period_are_sales_and_a_void_a_draft_and_another_month_are_not()
    {
        List<AnalysisLine> lines = LinesFor(Request(credits: false));

        // Seven lines: 9801 to 9807.
        Assert.Equal(7, lines.Count);
        Assert.Equal(80_000m + 3_000m, lines.Sum(l => l.Net));
    }

    [Fact]
    public void Credit_note_lines_are_taken_off_unless_turned_off()
    {
        Assert.Equal(83_000m - 3_000m, LinesFor(Request()).Sum(l => l.Net));
        Assert.Equal(83_000m, LinesFor(Request(credits: false)).Sum(l => l.Net));
    }

    [Fact]
    public void A_line_in_another_currency_is_taken_to_base_at_the_documents_rate()
    {
        AnalysisLine usd = LinesFor(Request(credits: false)).Single(l => l.ContactId == Acme);

        // USD 500 at 80.
        Assert.Equal((40_000m, 0m, 40_000m), (usd.Gross, usd.Discount, usd.Net));
    }

    [Fact]
    public void Net_sales_are_after_discount_and_gross_is_before_it()
    {
        AnalysisLine discounted = LinesFor(Request()).Single(l => l.Discount == 1_000m);

        Assert.Equal((15_000m, 1_000m, 14_000m), (discounted.Gross, discounted.Discount, discounted.Net));
    }

    [Fact]
    public void A_return_gives_units_value_and_cost_back_and_a_price_correction_only_value()
    {
        List<AnalysisLine> credits = [.. LinesFor(Request()).Where(l => l.Net < 0m).OrderBy(l => l.Net)];

        // The price correction first (-2,000), then the return (-1,000).
        Assert.Equal((0m, -2_000m, 0m, 0), (credits[0].Quantity, credits[0].Net, credits[0].Cost, credits[0].Pending));
        Assert.Equal((-1m, -1_000m, -500m, 0), (credits[1].Quantity, credits[1].Net, credits[1].Cost, credits[1].Pending));
    }

    [Fact]
    public void A_line_is_costed_by_the_movement_it_caused_and_one_not_costed_yet_is_pending_not_nothing()
    {
        List<AnalysisLine> lines = LinesFor(Request(credits: false));

        AnalysisLine costed = lines.Single(l => l.Net == 6_000m && l.ItemId == WidgetA);
        Assert.Equal((3_000m, 0), (costed.Cost, costed.Pending));
        AnalysisLine pending = lines.Single(l => l.Net == 5_000m);
        Assert.Equal((0m, 1), (pending.Cost, pending.Pending));
    }

    [Fact]
    public void A_line_with_no_movement_costs_nothing_and_is_not_pending()
    {
        AnalysisLine service = LinesFor(Request(credits: false)).Single(l => l.ItemId == 0);

        Assert.Equal((0m, 0, 0L), (service.Cost, service.Pending, service.CategoryId));
    }

    [Fact]
    public void A_movement_that_is_in_progress_or_failed_is_pending_too_and_a_skipped_one_is_not()
    {
        foreach ((CostingStatus status, int pending) in new[] { (CostingStatus.InProgress, 1), (CostingStatus.Failed, 1), (CostingStatus.Skipped, 0) })
        {
            StockMovementRead[] movements = [Movement("INV", 9403, 9805, null, status)];

            AnalysisLine line = SalesAnalysisReportRepository.Lines(
                Invoices.AsQueryable(), Details.AsQueryable(), CreditNotes.AsQueryable(), CreditDetails.AsQueryable(), Items.AsQueryable(), movements.AsQueryable(),
                Request(credits: false), October1, October31).Single(l => l.Net == 5_000m);

            Assert.Equal(pending, line.Pending);
        }
    }

    [Fact]
    public void A_movement_of_another_document_with_the_same_line_id_is_not_a_lines_cost()
    {
        StockMovementRead[] movements = [Movement("CRN", 9403, 9805, 999m)];

        AnalysisLine line = SalesAnalysisReportRepository.Lines(
            Invoices.AsQueryable(), Details.AsQueryable(), CreditNotes.AsQueryable(), CreditDetails.AsQueryable(), Items.AsQueryable(), movements.AsQueryable(),
            Request(credits: false), October1, October31).Single(l => l.Net == 5_000m);

        Assert.Equal((0m, 0), (line.Cost, line.Pending));
    }

    [Fact]
    public void The_customer_category_and_warehouse_each_narrow_the_lines()
    {
        Assert.Equal(40_000m, LinesFor(Request(contact: Acme)).Sum(l => l.Net));
        Assert.Equal(23_000m, LinesFor(Request(category: Electronics)).Sum(l => l.Net));
        Assert.Equal(51_000m, LinesFor(Request(warehouse: Main)).Sum(l => l.Net));
    }

    // ---- Groups ---------------------------------------------------------------------------

    [Fact]
    public void By_item_each_item_adds_up_its_lines_and_credits_and_lines_with_no_item_are_one_group()
    {
        List<AnalysisGroup> groups = GroupsFor(Request());

        Assert.Equal([0L, WidgetA, GadgetB], groups.Select(g => g.Key).Order());
        AnalysisGroup widget = Of(groups, WidgetA);
        Assert.Equal((20m, 51_000m, 0m, 51_000m, 4_500m, 1), (widget.Quantity, widget.Gross, widget.Discount, widget.Net, widget.Cost, widget.Pending));
        AnalysisGroup gadget = Of(groups, GadgetB);
        Assert.Equal((21m, 24_000m, 1_000m, 23_000m, 10_500m, 0), (gadget.Quantity, gadget.Gross, gadget.Discount, gadget.Net, gadget.Cost, gadget.Pending));
        AnalysisGroup none = Of(groups, 0);
        Assert.Equal((1m, 6_000m, 6_000m, 0m), (none.Quantity, none.Gross, none.Net, none.Cost));
    }

    [Fact]
    public void By_category_the_items_of_a_category_add_up_and_a_line_with_no_item_has_no_category()
    {
        List<AnalysisGroup> groups = GroupsFor(Request(SalesAnalysisGroupBy.Category));

        Assert.Equal([0L, Hardware, Electronics], groups.Select(g => g.Key).Order());
        Assert.Equal((51_000m, 4_500m), (Of(groups, Hardware).Net, Of(groups, Hardware).Cost));
        Assert.Equal((23_000m, 10_500m), (Of(groups, Electronics).Net, Of(groups, Electronics).Cost));
        Assert.Equal(6_000m, Of(groups, 0).Net);
    }

    [Fact]
    public void By_customer_each_customer_adds_up_everything_they_bought_less_what_they_returned()
    {
        List<AnalysisGroup> groups = GroupsFor(Request(SalesAnalysisGroupBy.Customer));

        // Zenith: 6,000 + 4,000 + 14,000 + 6,000 + 5,000 + 8,000 less 1,000 and 2,000.
        Assert.Equal(40_000m, Of(groups, Zenith).Net);
        Assert.Equal(40_000m, Of(groups, Acme).Net);
    }

    [Fact]
    public void However_it_is_grouped_the_groups_add_up_to_the_same_total()
    {
        foreach (SalesAnalysisGroupBy by in Enum.GetValues<SalesAnalysisGroupBy>())
        {
            List<AnalysisGroup> groups = GroupsFor(Request(by));

            Assert.Equal(80_000m, groups.Sum(g => g.Net));
            Assert.Equal(42m, groups.Sum(g => g.Quantity));
        }
    }

    [Fact]
    public void The_total_adds_up_every_line()
    {
        AnalysisGroup total = SalesAnalysisReportRepository.Total(LinesFor(Request()).AsQueryable()).Single();

        Assert.Equal((42m, 81_000m, 1_000m, 80_000m, 15_000m, 1), (total.Quantity, total.Gross, total.Discount, total.Net, total.Cost, total.Pending));
    }

    // ---- Margin and change ----------------------------------------------------------------

    [Fact]
    public void A_margin_is_net_sales_less_cost_and_a_share_of_net_sales()
    {
        (decimal? margin, decimal? percent) = SalesAnalysisReportRepository.MarginOf(Of(GroupsFor(Request()), GadgetB));

        Assert.Equal((12_500m, 54.35m), (margin, percent));
    }

    [Fact]
    public void A_group_with_a_line_not_costed_yet_has_no_margin_rather_than_an_overstated_one()
    {
        (decimal? margin, decimal? percent) = SalesAnalysisReportRepository.MarginOf(Of(GroupsFor(Request()), WidgetA));

        Assert.Equal((null, null), (margin, percent));
    }

    [Fact]
    public void A_group_with_no_net_sales_has_a_margin_but_no_percentage()
    {
        (decimal? margin, decimal? percent) = SalesAnalysisReportRepository.MarginOf(new AnalysisGroup { Net = 0m, Cost = 0m });

        Assert.Equal((0m, null), (margin, percent));
    }

    [Fact]
    public void Net_sales_change_is_a_share_of_the_comparison_and_nothing_when_there_is_no_comparison_or_nothing_was_sold_in_it()
    {
        Assert.Equal(1_175m, SalesAnalysisReportRepository.ChangeOf(51_000m, 4_000m));
        Assert.Equal(-50m, SalesAnalysisReportRepository.ChangeOf(50m, 100m));
        Assert.Null(SalesAnalysisReportRepository.ChangeOf(51_000m, 0m));
        Assert.Null(SalesAnalysisReportRepository.ChangeOf(51_000m, null));
    }

    // ---- Compose --------------------------------------------------------------------------

    private static readonly List<LabelRef> Labels =
    [
        new() { Key = WidgetA, Code = "WIDGET-A", Name = "Widget A" },
        new() { Key = GadgetB, Code = "GADGET-B", Name = "Gadget B" },
    ];

    private static SalesAnalysisReportResultDto Run(SalesAnalysisReportRequest? request = null, bool compare = false)
    {
        request ??= Request();
        List<AnalysisGroup> groups = [.. GroupsFor(request).OrderByDescending(g => g.Net).ThenBy(g => g.Key)];
        AnalysisGroup total = SalesAnalysisReportRepository.Total(LinesFor(request).AsQueryable()).Single();
        List<AnalysisGroup> earlier = compare ? [new AnalysisGroup { Key = WidgetA, Net = 4_000m }] : [];

        return SalesAnalysisReportRepository.Compose(request, groups, groups.Count, Labels, earlier, total, compare ? 4_000m : null, "INR");
    }

    [Fact]
    public void The_groups_are_largest_net_sales_first_with_their_names_and_margins()
    {
        SalesAnalysisReportResultDto report = Run();

        Assert.Equal([WidgetA, GadgetB, 0L], report.Groups.Select(g => g.Key));
        SalesAnalysisGroupDto gadget = report.Groups[1];
        Assert.Equal(("GADGET-B", "Gadget B", 12_500m, 54.35m), (gadget.Code, gadget.Name, gadget.Margin, gadget.MarginPercent));
        Assert.Null(report.Groups[0].Margin);
        Assert.Equal(1, report.Groups[0].CostPendingLines);
    }

    [Fact]
    public void The_group_of_lines_with_no_item_category_or_customer_is_named_for_that()
    {
        Assert.Equal("(No item)", Run().Groups.Single(g => g.Key == 0).Name);
        Assert.Equal("(No category)", Run(Request(SalesAnalysisGroupBy.Category)).Groups.Single(g => g.Key == 0).Name);
        Assert.Equal("(No customer)", SalesAnalysisReportRepository.NoneLabel(SalesAnalysisGroupBy.Customer));
    }

    [Fact]
    public void The_totals_carry_their_own_margin_which_is_missing_while_any_cost_is_pending()
    {
        SalesAnalysisTotalsDto totals = Run().Totals;

        Assert.Equal((42m, 81_000m, 1_000m, 80_000m, 15_000m, 1), (totals.Quantity, totals.Gross, totals.Discount, totals.Net, totals.Cost, totals.CostPendingLines));
        Assert.Equal((null, null), (totals.Margin, totals.MarginPercent));
    }

    [Fact]
    public void A_comparison_period_gives_each_group_its_net_sales_then_and_how_they_changed_and_a_group_with_none_then_has_none()
    {
        SalesAnalysisReportResultDto report = Run(compare: true);

        SalesAnalysisGroupDto widget = report.Groups.Single(g => g.Key == WidgetA);
        Assert.Equal((4_000m, 1_175m), (widget.CompareNet, widget.ChangePercent));
        SalesAnalysisGroupDto gadget = report.Groups.Single(g => g.Key == GadgetB);
        Assert.Equal((0m, null), (gadget.CompareNet, gadget.ChangePercent));
        Assert.Equal((4_000m, 1_900m), (report.Totals.CompareNet, report.Totals.ChangePercent));
    }

    [Fact]
    public void Without_a_comparison_period_nothing_is_compared()
    {
        SalesAnalysisReportResultDto report = Run();

        Assert.All(report.Groups, g => Assert.Equal((null, null), (g.CompareNet, g.ChangePercent)));
        Assert.Equal((null, null), (report.Totals.CompareNet, report.Totals.ChangePercent));
    }

    [Fact]
    public void The_period_the_grouping_credit_notes_and_page_are_passed_on()
    {
        var request = Request(SalesAnalysisGroupBy.Customer, credits: false);
        request.PageNumber = 2;
        request.PageSize = 10;

        SalesAnalysisReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR", SalesAnalysisGroupBy.Customer, false), (report.FromDate, report.ToDate, report.BaseCurrencyCode, report.GroupBy, report.IncludesCreditNotes));
        Assert.Equal((2, 10, 2), (report.PageNumber, report.PageSize, report.TotalGroups));
    }

    // ---- Lookups ---------------------------------------------------------------------------

    [Fact]
    public void The_warehouses_offered_are_the_active_ones_by_name()
    {
        WarehouseRead[] warehouses =
        [
            new() { WarehouseId = 1, WarehouseCode = "Z", WarehouseName = "Zeta", IsActive = true },
            new() { WarehouseId = 2, WarehouseCode = "A", WarehouseName = "Alpha", IsActive = true },
            new() { WarehouseId = 3, WarehouseCode = "G", WarehouseName = "Gone", IsActive = false },
        ];

        Assert.Equal(["Alpha", "Zeta"], SalesAnalysisReportRepository.WarehouseChoices(warehouses.AsQueryable()).Select(w => w.WarehouseName));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(SalesAnalysisReportRequest.FromDate), nameof(SalesAnalysisReportRequest.ToDate)],
            typeof(SalesAnalysisReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new SalesAnalysisReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new SalesAnalysisReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void By_default_it_groups_by_item_with_credit_notes_on()
    {
        var request = new SalesAnalysisReportRequest();

        Assert.Equal((SalesAnalysisGroupBy.Item, true), (request.GroupBy, request.IncludeCreditNotes));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_an_unknown_grouping_and_half_a_comparison_are_refused_at_their_fields()
    {
        Assert.Equal(
            [(nameof(SalesAnalysisReportRequest.ToDate), "To date cannot be before the from date.")],
            SalesAnalysisReportRepository.Refusals(Request(from: October31, to: October1)));

        Assert.Equal(
            [(nameof(SalesAnalysisReportRequest.GroupBy), "Group by must be one of the options offered.")],
            SalesAnalysisReportRepository.Refusals(Request((SalesAnalysisGroupBy)9)));

        var halfFrom = Request();
        halfFrom.CompareFromDate = new DateOnly(2026, 9, 1);
        Assert.Equal(
            [(nameof(SalesAnalysisReportRequest.CompareToDate), "Compare to date is required when a compare from date is given.")],
            SalesAnalysisReportRepository.Refusals(halfFrom));

        var halfTo = Request();
        halfTo.CompareToDate = new DateOnly(2026, 9, 30);
        Assert.Equal(
            [(nameof(SalesAnalysisReportRequest.CompareFromDate), "Compare from date is required when a compare to date is given.")],
            SalesAnalysisReportRepository.Refusals(halfTo));

        var reversed = Request();
        reversed.CompareFromDate = new DateOnly(2026, 9, 30);
        reversed.CompareToDate = new DateOnly(2026, 9, 1);
        Assert.Equal(
            [(nameof(SalesAnalysisReportRequest.CompareToDate), "Compare to date cannot be before the compare from date.")],
            SalesAnalysisReportRepository.Refusals(reversed));

        Assert.Empty(SalesAnalysisReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new SalesAnalysisReportController(null!)
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
            new SalesAnalysisReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(SalesAnalysisReportController);

        Assert.Equal("api/statements/sales-analysis", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[]
        {
            nameof(SalesAnalysisReportController.Run),
            nameof(SalesAnalysisReportController.Customers),
            nameof(SalesAnalysisReportController.ItemCategories),
            nameof(SalesAnalysisReportController.Warehouses),
        })
        {
            Assert.Contains(
                "sales.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("customers", controller.GetMethod(nameof(SalesAnalysisReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("item-categories", controller.GetMethod(nameof(SalesAnalysisReportController.ItemCategories))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("warehouses", controller.GetMethod(nameof(SalesAnalysisReportController.Warehouses))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new SalesAnalysisSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(["itemCode", "itemName", "categoryName", "quantity", "grossAmount"], source.Columns.Select(c => c.Key));
    }
}

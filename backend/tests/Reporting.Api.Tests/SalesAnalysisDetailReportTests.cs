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
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Sales Analysis Detail Report (TK-229), on the Sales Analysis's books (see
/// <see cref="SalesAnalysisReportTests"/>): the nine October lines of seven invoices and two credit notes, each with
/// the cost of the stock movement it caused. By item the lines fall in three groups: the line with no item (1 line, 6,000),
/// Gadget B (five lines: 4,000, 14,000, 8,000 and the return and price correction of -1,000 and -2,000, 23,000 in all,
/// cost 10,500, margin 12,500) and Widget A (three lines, 51,000, one pending its cost). Each group's subtotal is the
/// Sales Analysis's row for it. The builders run on lists through the same expressions the repository runs against the
/// read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class SalesAnalysisDetailReportTests
{
    private const long WidgetA = SalesAnalysisReportTests.WidgetA, GadgetB = SalesAnalysisReportTests.GadgetB;
    private const long Hardware = SalesAnalysisReportTests.Hardware, Electronics = SalesAnalysisReportTests.Electronics;
    private const long Zenith = SalesAnalysisReportTests.Zenith, Acme = SalesAnalysisReportTests.Acme, Main = SalesAnalysisReportTests.Main;

    private static readonly DateOnly October1 = SalesAnalysisReportTests.October1;
    private static readonly DateOnly October31 = SalesAnalysisReportTests.October31;

    private static SalesAnalysisDetailReportRequest Request(
        SalesAnalysisGroupBy by = SalesAnalysisGroupBy.Item, long? item = null, long? category = null, long? contact = null, long? warehouse = null,
        bool credits = true) =>
        new() { FromDate = October1, ToDate = October31, GroupBy = by, ItemId = item, ItemCategoryId = category, ContactId = contact, WarehouseId = warehouse, IncludeCreditNotes = credits };

    private static IQueryable<AnalysisLine> LinesFor(SalesAnalysisDetailReportRequest request) =>
        SalesAnalysisDetailReportRepository.Narrowed(
            SalesAnalysisReportRepository.Lines(
                SalesAnalysisReportTests.Invoices.AsQueryable(), SalesAnalysisReportTests.Details.AsQueryable(),
                SalesAnalysisReportTests.CreditNotes.AsQueryable(), SalesAnalysisReportTests.CreditDetails.AsQueryable(),
                SalesAnalysisReportTests.Items.AsQueryable(), SalesAnalysisReportTests.Movements.AsQueryable(),
                SalesAnalysisDetailReportRepository.AnalysisRequestOf(request), October1, October31),
            request);

    private static readonly List<ItemCategoryRead> Categories =
    [
        new() { ItemCategoryId = Hardware, CategoryCode = "HW", CategoryName = "Hardware", IsActive = true },
        new() { ItemCategoryId = Electronics, CategoryCode = "EL", CategoryName = "Electronics", IsActive = true },
    ];

    private static readonly List<ContactRead> Contacts =
    [
        new() { ContactId = Zenith, ContactCode = "ZENITH", DisplayName = "Zenith Traders" },
        new() { ContactId = Acme, ContactCode = "ACME", DisplayName = "Acme Exports" },
    ];

    private static List<AnalysisLine> OrderedFor(SalesAnalysisDetailReportRequest request) =>
    [
        .. SalesAnalysisDetailReportRepository.Ordered(
            LinesFor(request), SalesAnalysisReportTests.Items.AsQueryable(), Categories.AsQueryable(), Contacts.AsQueryable(), request.GroupBy),
    ];

    private static readonly List<LabelRef> ItemLabels =
    [
        new() { Key = WidgetA, Code = "WIDGET-A", Name = "Widget A" },
        new() { Key = GadgetB, Code = "GADGET-B", Name = "Gadget B" },
    ];

    private static readonly List<LabelRef> ContactLabels =
    [
        new() { Key = Zenith, Code = "ZENITH", Name = "Zenith Traders" },
        new() { Key = Acme, Code = "ACME", Name = "Acme Exports" },
    ];

    private static SalesAnalysisDetailReportResultDto Run(SalesAnalysisDetailReportRequest? request = null, int skip = 0, int take = 50)
    {
        request ??= Request();
        IQueryable<AnalysisLine> lines = LinesFor(request);
        List<AnalysisLine> page = [.. OrderedFor(request).Skip(skip).Take(take)];
        long[] keys = [.. page.Select(l => SalesAnalysisDetailReportRepository.KeyOf(l, request.GroupBy)).Distinct()];
        List<AnalysisGroup> subtotals = [.. SalesAnalysisReportRepository.Groups(lines, request.GroupBy).Where(g => keys.Contains(g.Key))];
        AnalysisGroup totals = SalesAnalysisReportRepository.Total(lines).Single();
        List<LabelRef> groupLabels = request.GroupBy switch
        {
            SalesAnalysisGroupBy.Item => ItemLabels,
            SalesAnalysisGroupBy.Customer => ContactLabels,
            _ => [new() { Key = Hardware, Code = "HW", Name = "Hardware" }, new() { Key = Electronics, Code = "EL", Name = "Electronics" }],
        };

        return SalesAnalysisDetailReportRepository.Compose(request, page, totals, subtotals, groupLabels, ItemLabels, ContactLabels, "INR");
    }

    // ---- The lines -----------------------------------------------------------------------

    [Fact]
    public void The_lines_are_the_sales_analysis_own_lines_with_their_documents()
    {
        List<AnalysisLine> lines = [.. LinesFor(Request())];

        Assert.Equal(9, lines.Count);
        Assert.Equal(80_000m, lines.Sum(l => l.Net));
        AnalysisLine line = lines.Single(l => l.DocumentNo == "INV-9402" && l.ItemId == GadgetB);
        Assert.Equal(("INV", 9402L, new DateOnly(2026, 10, 5), 1, 1_500m), (line.TypeCode, line.DocumentId, line.DocumentDate, line.LineNumber, line.UnitPrice));
    }

    [Fact]
    public void A_credit_note_line_is_its_own_row_with_the_credit_notes_number_and_a_negative_key()
    {
        AnalysisLine credit = LinesFor(Request()).Single(l => l.DocumentNo == "CRN-9421");

        Assert.Equal(("CRN", 9421L, -9901L, -1m, -1_000m), (credit.TypeCode, credit.DocumentId, credit.LineKey, credit.Quantity, credit.Net));
    }

    [Fact]
    public void Every_line_has_a_key_of_its_own_across_invoices_and_credit_notes()
    {
        List<long> keys = [.. LinesFor(Request()).Select(l => l.LineKey)];

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void A_line_in_another_currency_has_its_unit_price_in_base()
    {
        AnalysisLine usd = LinesFor(Request()).Single(l => l.DocumentNo == "INV-9405");

        // USD 100 at 80.
        Assert.Equal((8_000m, 40_000m), (usd.UnitPrice, usd.Net));
    }

    [Fact]
    public void An_item_narrows_the_lines_to_those_for_it()
    {
        List<AnalysisLine> lines = [.. LinesFor(Request(item: WidgetA))];

        Assert.Equal(3, lines.Count);
        Assert.All(lines, l => Assert.Equal(WidgetA, l.ItemId));
    }

    [Fact]
    public void The_sales_analysis_filters_apply_to_the_detail_too_and_credit_notes_can_be_left_out()
    {
        Assert.Equal(7, LinesFor(Request(credits: false)).Count());
        Assert.Equal(1, LinesFor(Request(contact: Acme)).Count());
        Assert.Equal(3, LinesFor(Request(warehouse: Main)).Count());
        Assert.Equal(5, LinesFor(Request(category: Electronics)).Count());
    }

    [Fact]
    public void The_analysis_request_carries_the_filters_the_two_reports_share()
    {
        var request = Request(item: WidgetA, category: Hardware, contact: Acme, warehouse: Main, credits: false);

        SalesAnalysisReportRequest analysis = SalesAnalysisDetailReportRepository.AnalysisRequestOf(request);

        Assert.Equal((October1, October31, Acme, Hardware, Main, false), (analysis.FromDate, analysis.ToDate, analysis.ContactId, analysis.ItemCategoryId, analysis.WarehouseId, analysis.IncludeCreditNotes));
    }

    // ---- Order ---------------------------------------------------------------------------

    [Fact]
    public void By_item_the_lines_are_ordered_by_item_name_then_date_and_the_line_with_no_item_comes_first()
    {
        List<AnalysisLine> ordered = OrderedFor(Request());

        Assert.Equal([0L, GadgetB, GadgetB, GadgetB, GadgetB, GadgetB, WidgetA, WidgetA, WidgetA], ordered.Select(l => l.ItemId));
        Assert.Equal(
            ["INV-9401", "INV-9402", "INV-9404", "CRN-9421", "CRN-9422"],
            ordered.Where(l => l.ItemId == GadgetB).Select(l => l.DocumentNo));
    }

    [Fact]
    public void By_customer_the_lines_are_ordered_by_customer_name_then_date()
    {
        List<AnalysisLine> ordered = OrderedFor(Request(SalesAnalysisGroupBy.Customer));

        // Acme Exports before Zenith Traders.
        Assert.Equal([Acme, Zenith], ordered.Select(l => l.ContactId).Distinct());
        Assert.Equal(["INV-9405"], ordered.Where(l => l.ContactId == Acme).Select(l => l.DocumentNo));
    }

    [Fact]
    public void By_category_the_lines_are_ordered_by_category_name_and_a_line_with_none_comes_first()
    {
        List<AnalysisLine> ordered = OrderedFor(Request(SalesAnalysisGroupBy.Category));

        // (none), Electronics, Hardware.
        Assert.Equal([0L, Electronics, Hardware], ordered.Select(l => l.CategoryId).Distinct());
    }

    [Fact]
    public void A_line_is_grouped_under_its_item_category_or_customer()
    {
        AnalysisLine line = LinesFor(Request()).First(l => l.ItemId == GadgetB && l.ContactId == Zenith);

        Assert.Equal(GadgetB, SalesAnalysisDetailReportRepository.KeyOf(line, SalesAnalysisGroupBy.Item));
        Assert.Equal(Electronics, SalesAnalysisDetailReportRepository.KeyOf(line, SalesAnalysisGroupBy.Category));
        Assert.Equal(Zenith, SalesAnalysisDetailReportRepository.KeyOf(line, SalesAnalysisGroupBy.Customer));
    }

    [Fact]
    public void Lines_in_a_row_with_the_same_group_are_one_run_and_a_group_that_comes_back_is_a_run_of_its_own()
    {
        AnalysisLine[] lines =
        [
            new() { ItemId = 1 }, new() { ItemId = 1 }, new() { ItemId = 2 }, new() { ItemId = 1 },
        ];

        List<IGrouping<long, AnalysisLine>> runs = [.. SalesAnalysisDetailReportRepository.GroupAdjacent(lines, SalesAnalysisGroupBy.Item)];

        Assert.Equal([(1L, 2), (2L, 1), (1L, 1)], runs.Select(r => (r.Key, r.Count())));
    }

    // ---- Compose --------------------------------------------------------------------------

    [Fact]
    public void The_lines_are_gathered_into_their_groups_each_with_its_subtotal_over_every_line()
    {
        SalesAnalysisDetailReportResultDto report = Run();

        Assert.Equal([0L, GadgetB, WidgetA], report.Groups.Select(g => g.Key));
        SalesAnalysisDetailGroupDto gadget = report.Groups[1];
        Assert.Equal(("GADGET-B", "Gadget B", 5, 5), (gadget.Code, gadget.Name, gadget.LineCount, gadget.Lines.Count));
        Assert.Equal((21m, 24_000m, 1_000m, 23_000m, 10_500m, 12_500m, 54.35m),
            (gadget.Subtotal.Quantity, gadget.Subtotal.Gross, gadget.Subtotal.Discount, gadget.Subtotal.Net, gadget.Subtotal.Cost, gadget.Subtotal.Margin, gadget.Subtotal.MarginPercent));
    }

    [Fact]
    public void A_group_whose_lines_run_over_the_page_still_shows_the_subtotal_of_all_of_them()
    {
        // Four lines a page: page one is the line with no item and three of Gadget B's five.
        SalesAnalysisDetailReportResultDto first = Run(skip: 0, take: 4);
        SalesAnalysisDetailReportResultDto second = Run(skip: 4, take: 4);

        SalesAnalysisDetailGroupDto onFirst = first.Groups.Single(g => g.Key == GadgetB);
        SalesAnalysisDetailGroupDto onSecond = second.Groups.Single(g => g.Key == GadgetB);
        Assert.Equal((3, 2), (onFirst.Lines.Count, onSecond.Lines.Count));
        Assert.Equal((5, 5), (onFirst.LineCount, onSecond.LineCount));
        Assert.Equal(23_000m, onFirst.Subtotal.Net);
        Assert.Equal(23_000m, onSecond.Subtotal.Net);
    }

    [Fact]
    public void A_line_pending_its_cost_has_no_margin_and_neither_has_its_group()
    {
        SalesAnalysisDetailGroupDto widget = Run().Groups.Single(g => g.Key == WidgetA);

        SalesAnalysisDetailLineDto pending = widget.Lines.Single(l => l.CostPending);
        Assert.Equal(("INV-9403", null), (pending.DocumentNo, pending.Margin));
        Assert.Equal((1, null), (widget.Subtotal.CostPendingLines, widget.Subtotal.Margin));
        Assert.Equal(3_000m, widget.Lines.Single(l => l.DocumentNo == "INV-9401").Margin);
    }

    [Fact]
    public void A_line_carries_its_document_customer_item_and_figures()
    {
        SalesAnalysisDetailLineDto line = Run().Groups.Single(g => g.Key == GadgetB).Lines.Single(l => l.DocumentNo == "INV-9402");

        Assert.Equal(("INV", new DateOnly(2026, 10, 5), "Zenith Traders", "GADGET-B", "Gadget B"), (line.TypeCode, line.DocumentDate, line.ContactName, line.ItemCode, line.ItemName));
        Assert.Equal((10m, 1_500m, 15_000m, 1_000m, 14_000m, 5_000m, 9_000m), (line.Quantity, line.UnitPrice, line.Gross, line.Discount, line.Net, line.Cost, line.Margin));
    }

    [Fact]
    public void The_group_of_lines_with_no_item_is_named_for_that()
    {
        SalesAnalysisDetailGroupDto none = Run().Groups.Single(g => g.Key == 0);

        Assert.Equal(("(No item)", string.Empty, 1), (none.Name, none.Code, none.LineCount));
        Assert.Equal("(No item)", none.Lines.Single().ItemName);
    }

    [Fact]
    public void The_totals_are_the_analysis_totals_and_the_lines_counted()
    {
        SalesAnalysisDetailReportResultDto report = Run();

        Assert.Equal((42m, 81_000m, 1_000m, 80_000m, 15_000m, 1), (report.Totals.Quantity, report.Totals.Gross, report.Totals.Discount, report.Totals.Net, report.Totals.Cost, report.Totals.CostPendingLines));
        Assert.Equal(9, report.TotalLines);
        Assert.Null(report.Totals.Margin);
    }

    [Fact]
    public void However_it_is_grouped_the_subtotals_add_up_to_the_total()
    {
        foreach (SalesAnalysisGroupBy by in Enum.GetValues<SalesAnalysisGroupBy>())
        {
            SalesAnalysisDetailReportResultDto report = Run(Request(by));

            Assert.Equal(report.Totals.Net, report.Groups.Sum(g => g.Subtotal.Net));
            Assert.Equal(9, report.Groups.Sum(g => g.LineCount));
        }
    }

    [Fact]
    public void The_period_the_grouping_credit_notes_and_page_are_passed_on()
    {
        var request = Request(SalesAnalysisGroupBy.Customer, credits: false);
        request.PageNumber = 2;
        request.PageSize = 10;

        SalesAnalysisDetailReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR", SalesAnalysisGroupBy.Customer, false), (report.FromDate, report.ToDate, report.BaseCurrencyCode, report.GroupBy, report.IncludesCreditNotes));
        Assert.Equal((2, 10, 7), (report.PageNumber, report.PageSize, report.TotalLines));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(SalesAnalysisDetailReportRequest.FromDate), nameof(SalesAnalysisDetailReportRequest.ToDate)],
            typeof(SalesAnalysisDetailReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new SalesAnalysisDetailReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new SalesAnalysisDetailReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void By_default_it_groups_by_item_with_credit_notes_on()
    {
        var request = new SalesAnalysisDetailReportRequest();

        Assert.Equal((SalesAnalysisGroupBy.Item, true), (request.GroupBy, request.IncludeCreditNotes));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_and_an_unknown_grouping_are_refused_at_their_fields()
    {
        Assert.Equal(
            [(nameof(SalesAnalysisDetailReportRequest.ToDate), "To date cannot be before the from date.")],
            SalesAnalysisDetailReportRepository.Refusals(new SalesAnalysisDetailReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(SalesAnalysisDetailReportRequest.GroupBy), "Group by must be one of the options offered.")],
            SalesAnalysisDetailReportRepository.Refusals(Request((SalesAnalysisGroupBy)9)));

        Assert.Empty(SalesAnalysisDetailReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new SalesAnalysisDetailReportController(null!)
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
            new SalesAnalysisDetailReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(SalesAnalysisDetailReportController);

        Assert.Equal("api/statements/sales-analysis-detail-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[]
        {
            nameof(SalesAnalysisDetailReportController.Run),
            nameof(SalesAnalysisDetailReportController.Customers),
            nameof(SalesAnalysisDetailReportController.Items),
            nameof(SalesAnalysisDetailReportController.ItemCategories),
            nameof(SalesAnalysisDetailReportController.Warehouses),
        })
        {
            Assert.Contains(
                "sales.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("customers", controller.GetMethod(nameof(SalesAnalysisDetailReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("items", controller.GetMethod(nameof(SalesAnalysisDetailReportController.Items))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("item-categories", controller.GetMethod(nameof(SalesAnalysisDetailReportController.ItemCategories))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("warehouses", controller.GetMethod(nameof(SalesAnalysisDetailReportController.Warehouses))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new SalesAnalysisDetailSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentDate", "documentNo", "itemCode", "itemName", "quantity", "unitPrice", "lineTotal"],
            source.Columns.Select(c => c.Key));
    }
}

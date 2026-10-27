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
/// The Delivery Order Track Report (TK-232), on challans worked by hand. The branch's base currency is INR and the
/// period is October 2026. Widget A is delivered at 600 and Gadget B at 1,000:
/// <list type="bullet">
/// <item><b>DLC-9701</b> (Zenith, a sale against SOR-9501): Widget A 10 and Gadget B 5, nothing billed. <b>Not invoiced</b>, 11,000.</item>
/// <item><b>DLC-9702</b> (Zenith, a sale against SOR-9502): Widget A 4 and Gadget B 4, all billed on INV-9402. <b>Invoiced</b>, 6,400.</item>
/// <item><b>DLC-9703</b> (Acme, a sale in US dollars at 80 against SOR-9503): Widget A 5 at USD 100 billed in full, Gadget B 4 at USD 50
/// with 1 billed, on INV-9405. <b>Partly invoiced</b>: 56,000 delivered, 44,000 invoiced, 12,000 to bill.</item>
/// <item><b>DLC-9704</b> (Zenith, job work): Widget A 3, nothing billed. <b>Not invoiced</b>, 1,800.</item>
/// <item><b>DLC-9705</b> (Zenith, on approval): Gadget B 2, kept and billed on INV-9401. <b>Invoiced</b>, 2,000.</item>
/// <item><b>DLC-9706</b> (Acme, a sample): Widget A 1, nothing billed. <b>Not invoiced</b>, 600.</item>
/// <item><b>DLC-9707</b> (Zenith, a sale against SOR-9504): Widget A 10 with 6 billed, on INV-9403 and INV-9404 (a voided INV-9406 is
/// not counted). <b>Partly invoiced</b>: 6,000 delivered, 3,600 invoiced, 2,400 to bill.</item>
/// <item>A voided challan (<b>void</b>), a draft, and a challan dated in September, which are not in the default list.</item>
/// </list>
/// The seven challans in the default list come to 83,800 delivered, 56,000 invoiced and 27,800 still to bill. The builders
/// run on lists through the same expressions the repository runs against the read models; the composition is pure.
/// Nothing here needs PostgreSQL.
/// </summary>
public sealed class DeliveryOrderTrackReportTests
{
    private const long Acme = 9101, Zenith = 9103, WidgetA = 9601, GadgetB = 9602;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static DeliveryChallanRead Challan(
        long id, DateOnly date, long contact, ChallanType type, string currency, decimal rate, long? order, DocumentStatus status = DocumentStatus.Posted) =>
        new()
        {
            DeliveryChallanId = id, TransactionTypeCode = "DLC", DocumentNo = $"DLC-{id}", DocumentDate = date, DispatchDate = date.AddDays(1), ContactId = contact,
            ChallanType = type, CurrencyCode = currency, ExchangeRate = rate, SalesOrderId = order, Status = status,
        };

    private static readonly List<DeliveryChallanRead> Challans =
    [
        Challan(9701, new(2026, 10, 2), Zenith, ChallanType.Sale, "INR", 1m, 9501),
        Challan(9702, new(2026, 10, 5), Zenith, ChallanType.Sale, "INR", 1m, 9502),
        Challan(9703, new(2026, 10, 9), Acme, ChallanType.Sale, "USD", 80m, 9503),
        Challan(9704, new(2026, 10, 12), Zenith, ChallanType.JobWork, "INR", 1m, null),
        Challan(9705, new(2026, 10, 14), Zenith, ChallanType.Approval, "INR", 1m, null),
        Challan(9706, new(2026, 10, 18), Acme, ChallanType.Sample, "INR", 1m, null),
        Challan(9707, new(2026, 10, 20), Zenith, ChallanType.Sale, "INR", 1m, 9504),
        Challan(9708, new(2026, 10, 22), Zenith, ChallanType.Sale, "INR", 1m, null, DocumentStatus.Void),
        Challan(9709, new(2026, 10, 23), Zenith, ChallanType.Sale, "INR", 1m, null, DocumentStatus.Draft),
        Challan(9710, new(2026, 9, 20), Zenith, ChallanType.Sale, "INR", 1m, null),
    ];

    private static DeliveryChallanDetailRead Line(long id, long challan, int number, long item, decimal qty, decimal price, decimal invoiced) =>
        new()
        {
            DeliveryChallanDetailId = id, DeliveryChallanId = challan, LineNumber = number, ItemId = item, UomId = 40, Quantity = qty, UnitPrice = price,
            TaxableAmount = qty * price, InvoicedQuantity = invoiced,
        };

    private static readonly List<DeliveryChallanDetailRead> Details =
    [
        Line(9711, 9701, 1, WidgetA, 10m, 600m, 0m),
        Line(9712, 9701, 2, GadgetB, 5m, 1_000m, 0m),
        Line(9721, 9702, 1, WidgetA, 4m, 600m, 4m),
        Line(9722, 9702, 2, GadgetB, 4m, 1_000m, 4m),
        Line(9731, 9703, 1, WidgetA, 5m, 100m, 5m),
        Line(9732, 9703, 2, GadgetB, 4m, 50m, 1m),
        Line(9741, 9704, 1, WidgetA, 3m, 600m, 0m),
        Line(9751, 9705, 1, GadgetB, 2m, 1_000m, 2m),
        Line(9761, 9706, 1, WidgetA, 1m, 600m, 0m),
        Line(9771, 9707, 1, WidgetA, 10m, 600m, 6m),
        Line(9781, 9708, 1, WidgetA, 1m, 600m, 0m),
        Line(9791, 9709, 1, WidgetA, 1m, 600m, 0m),
        Line(9801, 9710, 1, WidgetA, 1m, 600m, 0m),
    ];

    private static readonly List<ItemRead> Items =
    [
        new() { ItemId = WidgetA, ItemCode = "WIDGET-A", ItemName = "Widget A" },
        new() { ItemId = GadgetB, ItemCode = "GADGET-B", ItemName = "Gadget B" },
    ];

    private static readonly List<UnitOfMeasureRead> Units = [new() { UomId = 40, UomCode = "PCS" }];

    private static InvoiceRead Invoice(long id, DateOnly date, long? challan, DocumentStatus status = DocumentStatus.Posted) =>
        new() { InvoiceId = id, TransactionTypeCode = "INV", DocumentNo = $"INV-{id}", DocumentDate = date, DeliveryChallanId = challan, Status = status };

    private static readonly List<InvoiceRead> Invoices =
    [
        Invoice(9402, new(2026, 10, 5), 9702),
        Invoice(9405, new(2026, 10, 18), 9703),
        Invoice(9401, new(2026, 10, 2), 9705),
        Invoice(9404, new(2026, 10, 15), 9707),
        Invoice(9403, new(2026, 10, 10), 9707),
        Invoice(9406, new(2026, 10, 20), 9707, DocumentStatus.Void),
        Invoice(9408, new(2026, 9, 20), null),
    ];

    private static readonly List<SalesOrderRead> Orders =
    [
        new() { SalesOrderId = 9501, DocumentNo = "SOR-9501" },
        new() { SalesOrderId = 9502, DocumentNo = "SOR-9502" },
        new() { SalesOrderId = 9503, DocumentNo = "SOR-9503" },
        new() { SalesOrderId = 9504, DocumentNo = "SOR-9504" },
        new() { SalesOrderId = 9599, DocumentNo = "SOR-9599" },
    ];

    private static readonly List<CustomerRef> Customers =
    [
        new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Traders" },
        new() { ContactId = Acme, Code = "ACME", Name = "Acme Exports" },
    ];

    private static DeliveryOrderTrackReportRequest Request(
        long? contact = null, ChallanProgress? progress = null, ChallanType? type = null, bool notInvoiced = false, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ContactId = contact, Progress = progress, ChallanType = type, NotInvoicedOnly = notInvoiced };

    private static List<ChallanTrackRow> RowsFor(DeliveryOrderTrackReportRequest request)
    {
        IQueryable<DeliveryChallanRead> challans = DeliveryOrderTrackReportRepository.Challans(Challans.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value);
        return [.. DeliveryOrderTrackReportRepository.Rows(challans, DeliveryOrderTrackReportRepository.Figures(challans, Details.AsQueryable()))];
    }

    private static ChallanTrackRow RowOf(IEnumerable<ChallanTrackRow> rows, long id) => rows.Single(r => r.DeliveryChallanId == id);

    private static DeliveryOrderTrackReportResultDto Run(DeliveryOrderTrackReportRequest? request = null, List<ChallanProgressCount>? counts = null)
    {
        request ??= Request();
        IQueryable<DeliveryChallanRead> challans = DeliveryOrderTrackReportRepository.Challans(Challans.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value);
        IQueryable<ChallanTrackRow> rows = DeliveryOrderTrackReportRepository.Rows(challans, DeliveryOrderTrackReportRepository.Figures(challans, Details.AsQueryable()));
        IQueryable<ChallanTrackRow> counted = DeliveryOrderTrackReportRepository.NotInvoicedOnly(rows, request.NotInvoicedOnly);
        IQueryable<ChallanTrackRow> matching = DeliveryOrderTrackReportRepository.WithProgress(counted, request.Progress);
        List<ChallanTrackRow> page = [.. matching.OrderByDescending(r => r.DocumentDate).ThenByDescending(r => r.DeliveryChallanId)];
        long[] ids = [.. page.Select(r => r.DeliveryChallanId)];
        long[] orderIds = [.. page.Where(r => r.SalesOrderId != null).Select(r => r.SalesOrderId!.Value)];

        return DeliveryOrderTrackReportRepository.Compose(
            request,
            page,
            page.Count,
            [.. DeliveryOrderTrackReportRepository.Lines(Challans.AsQueryable(), Details.AsQueryable(), Items.AsQueryable(), Units.AsQueryable(), ids)],
            [.. DeliveryOrderTrackReportRepository.Raised(Invoices.AsQueryable(), ids)],
            [.. DeliveryOrderTrackReportRepository.SourceOrders(Orders.AsQueryable(), orderIds)],
            Customers,
            counts ?? [.. counted.GroupBy(r => r.Progress).Select(g => new ChallanProgressCount { Progress = g.Key, Count = g.Count() })],
            new ChallanTrackRow { DeliveredValue = matching.Sum(r => r.DeliveredValue), InvoicedValue = matching.Sum(r => r.InvoicedValue) },
            "INR");
    }

    // ---- Which challans ---------------------------------------------------------------------

    [Fact]
    public void Posted_and_voided_challans_in_the_period_are_listed_and_a_draft_and_another_month_are_not()
    {
        Assert.Equal([9701L, 9702, 9703, 9704, 9705, 9706, 9707, 9708], RowsFor(Request()).Select(r => r.DeliveryChallanId).Order());
    }

    [Fact]
    public void The_customer_and_the_challan_type_each_narrow_the_challans()
    {
        Assert.Equal([9703L, 9706], RowsFor(Request(contact: Acme)).Select(r => r.DeliveryChallanId).Order());
        Assert.Equal([9704L], RowsFor(Request(type: ChallanType.JobWork)).Select(r => r.DeliveryChallanId));
        Assert.Equal([9701L, 9702, 9703, 9707, 9708], RowsFor(Request(type: ChallanType.Sale)).Select(r => r.DeliveryChallanId).Order());
        Assert.Equal([9706L], RowsFor(Request(type: ChallanType.Sample)).Select(r => r.DeliveryChallanId));
    }

    [Fact]
    public void A_sample_is_a_challan_type_the_report_can_read()
    {
        Assert.Contains(nameof(ChallanType.Sample), Enum.GetNames<ChallanType>());
        Assert.Equal(4, (int)ChallanType.Sample);
    }

    // ---- Status -----------------------------------------------------------------------------

    [Theory]
    [InlineData(9701L, ChallanProgress.NotInvoiced)]
    [InlineData(9702L, ChallanProgress.Invoiced)]
    [InlineData(9703L, ChallanProgress.PartlyInvoiced)]
    [InlineData(9704L, ChallanProgress.NotInvoiced)]
    [InlineData(9705L, ChallanProgress.Invoiced)]
    [InlineData(9706L, ChallanProgress.NotInvoiced)]
    [InlineData(9707L, ChallanProgress.PartlyInvoiced)]
    [InlineData(9708L, ChallanProgress.Void)]
    public void Each_challan_has_the_status_worked_out_by_hand(long id, ChallanProgress expected)
    {
        Assert.Equal(expected, RowOf(RowsFor(Request()), id).Progress);
    }

    [Fact]
    public void One_line_billed_and_another_not_is_partly_invoiced_and_one_line_part_billed_is_too()
    {
        // 9703: Widget A billed in full, Gadget B 1 of 4. 9707: its one line 6 of 10.
        List<ChallanTrackRow> rows = RowsFor(Request());

        Assert.Equal(ChallanProgress.PartlyInvoiced, RowOf(rows, 9703).Progress);
        Assert.Equal(ChallanProgress.PartlyInvoiced, RowOf(rows, 9707).Progress);
    }

    [Fact]
    public void A_voided_challan_is_void_whatever_its_lines_say()
    {
        Details.Add(Line(9782, 9708, 2, GadgetB, 1m, 1_000m, 1m));
        try
        {
            Assert.Equal(ChallanProgress.Void, RowOf(RowsFor(Request()), 9708).Progress);
        }
        finally
        {
            Details.RemoveAll(d => d.DeliveryChallanDetailId == 9782);
        }
    }

    // ---- Values -----------------------------------------------------------------------------

    [Fact]
    public void A_challans_values_are_its_lines_added_up_in_base_currency_at_its_own_prices()
    {
        List<ChallanTrackRow> rows = RowsFor(Request());

        Assert.Equal((11_000m, 0m), (RowOf(rows, 9701).DeliveredValue, RowOf(rows, 9701).InvoicedValue));
        Assert.Equal((6_400m, 6_400m), (RowOf(rows, 9702).DeliveredValue, RowOf(rows, 9702).InvoicedValue));
        Assert.Equal((56_000m, 44_000m), (RowOf(rows, 9703).DeliveredValue, RowOf(rows, 9703).InvoicedValue));
        Assert.Equal((6_000m, 3_600m), (RowOf(rows, 9707).DeliveredValue, RowOf(rows, 9707).InvoicedValue));
    }

    [Fact]
    public void Delivered_less_invoiced_is_still_to_bill_on_every_line_and_every_challan()
    {
        DeliveryOrderTrackReportResultDto report = Run();

        foreach (DeliveryOrderTrackChallanDto challan in report.Challans)
        {
            Assert.Equal(challan.DeliveredValue - challan.InvoicedValue, challan.ToBillValue);
            Assert.Equal(challan.ToBillValue, challan.Lines.Sum(l => l.ToBillValue));
            Assert.Equal(challan.DeliveredValue, challan.Lines.Sum(l => l.DeliveredValue));
            Assert.Equal(challan.InvoicedValue, challan.Lines.Sum(l => l.InvoicedValue));

            foreach (DeliveryOrderTrackLineDto line in challan.Lines)
            {
                Assert.Equal(line.Delivered - line.Invoiced, line.ToBill);
                Assert.Equal(line.DeliveredValue - line.InvoicedValue, line.ToBillValue);
            }
        }
    }

    [Fact]
    public void The_totals_of_every_match_and_what_is_still_to_bill_are_worked_out_from_them()
    {
        DeliveryOrderTrackTotalsDto totals = Run().Totals;

        Assert.Equal((83_800m, 56_000m, 27_800m), (totals.DeliveredValue, totals.InvoicedValue, totals.ToBillValue));
    }

    [Fact]
    public void The_total_to_bill_is_the_sum_of_the_challans_to_bill()
    {
        DeliveryOrderTrackReportResultDto report = Run();

        Assert.Equal(report.Challans.Sum(c => c.ToBillValue), report.Totals.ToBillValue);
    }

    // ---- The filters ------------------------------------------------------------------------

    [Fact]
    public void With_no_status_asked_for_every_challan_but_the_void_ones_is_listed_and_the_void_ones_only_when_asked()
    {
        Assert.Equal(7, Run().Challans.Count);
        Assert.DoesNotContain(Run().Challans, c => c.Progress == ChallanProgress.Void);
        Assert.Equal([9708L], Run(Request(progress: ChallanProgress.Void)).Challans.Select(c => c.DeliveryChallanId));
        Assert.Equal([9703L, 9707], Run(Request(progress: ChallanProgress.PartlyInvoiced)).Challans.Select(c => c.DeliveryChallanId).Order());
    }

    [Fact]
    public void Not_yet_invoiced_only_keeps_a_challan_with_something_still_to_bill()
    {
        DeliveryOrderTrackReportResultDto report = Run(Request(notInvoiced: true));

        Assert.Equal([9701L, 9703, 9704, 9706, 9707], report.Challans.Select(c => c.DeliveryChallanId).Order());
        Assert.DoesNotContain(report.Challans, c => c.Progress is ChallanProgress.Invoiced or ChallanProgress.Void);
        Assert.Equal((0, 0), (report.ProgressCounts[ChallanProgress.Invoiced], report.ProgressCounts[ChallanProgress.Void]));
    }

    [Fact]
    public void The_tab_counts_ignore_the_status_chosen_but_follow_the_other_filters()
    {
        DeliveryOrderTrackReportResultDto chosen = Run(Request(progress: ChallanProgress.Invoiced));
        Assert.Equal(
            (3, 2, 2, 1),
            (chosen.ProgressCounts[ChallanProgress.NotInvoiced], chosen.ProgressCounts[ChallanProgress.PartlyInvoiced],
                chosen.ProgressCounts[ChallanProgress.Invoiced], chosen.ProgressCounts[ChallanProgress.Void]));

        DeliveryOrderTrackReportResultDto sales = Run(Request(type: ChallanType.Sale));
        Assert.Equal(
            (1, 2, 1, 1),
            (sales.ProgressCounts[ChallanProgress.NotInvoiced], sales.ProgressCounts[ChallanProgress.PartlyInvoiced],
                sales.ProgressCounts[ChallanProgress.Invoiced], sales.ProgressCounts[ChallanProgress.Void]));
    }

    [Fact]
    public void Every_status_has_a_count_a_nought_included()
    {
        DeliveryOrderTrackReportResultDto report = Run(counts: [new() { Progress = ChallanProgress.Invoiced, Count = 4 }]);

        Assert.Equal(Enum.GetValues<ChallanProgress>().Length, report.ProgressCounts.Count);
        Assert.Equal(4, report.ProgressCounts[ChallanProgress.Invoiced]);
        Assert.Equal(0, report.ProgressCounts[ChallanProgress.NotInvoiced]);
    }

    // ---- The page ---------------------------------------------------------------------------

    [Fact]
    public void Challans_are_listed_newest_first_with_their_customers_types_and_dispatch_dates()
    {
        DeliveryOrderTrackReportResultDto report = Run();

        Assert.Equal([9707L, 9706, 9705, 9704, 9703, 9702, 9701], report.Challans.Select(c => c.DeliveryChallanId));
        Assert.Equal(("ZENITH", "Zenith Traders", ChallanType.Sale), (report.Challans[0].ContactCode, report.Challans[0].ContactName, report.Challans[0].ChallanType));
        Assert.Equal(("Acme Exports", ChallanType.Sample), (report.Challans[1].ContactName, report.Challans[1].ChallanType));
        Assert.Equal(new DateOnly(2026, 10, 21), report.Challans[0].DispatchDate);
    }

    [Fact]
    public void A_challan_names_the_sales_order_it_was_delivered_against_and_one_with_no_order_names_none()
    {
        DeliveryOrderTrackReportResultDto report = Run();

        Assert.Equal(("SOR-9504", 9504L), (report.Challans.Single(c => c.DeliveryChallanId == 9707).SalesOrderNo, report.Challans.Single(c => c.DeliveryChallanId == 9707).SalesOrderId));
        Assert.Equal("SOR-9501", report.Challans.Single(c => c.DeliveryChallanId == 9701).SalesOrderNo);
        Assert.Null(report.Challans.Single(c => c.DeliveryChallanId == 9704).SalesOrderNo);
        Assert.Null(report.Challans.Single(c => c.DeliveryChallanId == 9704).SalesOrderId);
    }

    [Fact]
    public void A_challan_lists_the_posted_invoices_raised_from_it_earliest_first_and_not_a_voided_one()
    {
        DeliveryOrderTrackReportResultDto report = Run();

        Assert.Equal(["INV-9403", "INV-9404"], report.Challans.Single(c => c.DeliveryChallanId == 9707).Invoices.Select(i => i.DocumentNo));
        Assert.Equal(["INV-9405"], report.Challans.Single(c => c.DeliveryChallanId == 9703).Invoices.Select(i => i.DocumentNo));
        Assert.Empty(report.Challans.Single(c => c.DeliveryChallanId == 9701).Invoices);
        Assert.All(report.Challans.SelectMany(c => c.Invoices), i => Assert.Equal("INV", i.TypeCode));
    }

    [Fact]
    public void Lines_carry_their_items_units_and_figures_in_line_order()
    {
        DeliveryOrderTrackLineDto[] lines = [.. Run().Challans.Single(c => c.DeliveryChallanId == 9703).Lines];

        Assert.Equal([1, 2], lines.Select(l => l.LineNumber));
        Assert.Equal(("WIDGET-A", "Widget A", "PCS", 5m, 5m, 0m), (lines[0].ItemCode, lines[0].ItemName, lines[0].UnitCode, lines[0].Delivered, lines[0].Invoiced, lines[0].ToBill));
        Assert.Equal((4m, 1m, 3m, 16_000m, 4_000m, 12_000m), (lines[1].Delivered, lines[1].Invoiced, lines[1].ToBill, lines[1].DeliveredValue, lines[1].InvoicedValue, lines[1].ToBillValue));
    }

    [Fact]
    public void A_line_for_no_item_reads_as_its_own_description()
    {
        DeliveryOrderTrackReportResultDto report = DeliveryOrderTrackReportRepository.Compose(
            Request(),
            [new() { DeliveryChallanId = 1, DocumentNo = "DLC-1", DocumentDate = October1, ContactId = Zenith, CurrencyCode = "INR" }],
            1,
            [new() { DeliveryChallanId = 1, LineNumber = 1, Description = "Installation", Delivered = 1m }],
            [], [], Customers, [], new(), "INR");

        Assert.Equal(("Installation", null), (report.Challans[0].Lines[0].ItemName, report.Challans[0].Lines[0].ItemCode));
    }

    [Fact]
    public void A_customer_the_names_do_not_know_is_still_listed_by_its_number()
    {
        DeliveryOrderTrackReportResultDto report = DeliveryOrderTrackReportRepository.Compose(
            Request(), [new() { DeliveryChallanId = 1, DocumentNo = "DLC-1", DocumentDate = October1, ContactId = 77, CurrencyCode = "INR" }], 1, [], [], [], [], [], new(), "INR");

        Assert.Equal("Customer 77", report.Challans[0].ContactName);
    }

    [Fact]
    public void The_period_and_page_are_passed_on()
    {
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        DeliveryOrderTrackReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 7), (report.PageNumber, report.PageSize, report.TotalChallans));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(DeliveryOrderTrackReportRequest.FromDate), nameof(DeliveryOrderTrackReportRequest.ToDate)],
            typeof(DeliveryOrderTrackReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new DeliveryOrderTrackReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new DeliveryOrderTrackReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_and_an_unknown_status_or_type_are_refused_at_their_fields()
    {
        Assert.Equal(
            [(nameof(DeliveryOrderTrackReportRequest.ToDate), "To date cannot be before the from date.")],
            DeliveryOrderTrackReportRepository.Refusals(new DeliveryOrderTrackReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(DeliveryOrderTrackReportRequest.Progress), "Status must be one of the options offered.")],
            DeliveryOrderTrackReportRepository.Refusals(Request(progress: (ChallanProgress)9)));

        Assert.Equal(
            [(nameof(DeliveryOrderTrackReportRequest.ChallanType), "Challan type must be one of the options offered.")],
            DeliveryOrderTrackReportRepository.Refusals(Request(type: (ChallanType)9)));

        Assert.Empty(DeliveryOrderTrackReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new DeliveryOrderTrackReportController(null!)
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
            new DeliveryOrderTrackReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(DeliveryOrderTrackReportController);

        Assert.Equal("api/statements/delivery-order-track-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(DeliveryOrderTrackReportController.Run), nameof(DeliveryOrderTrackReportController.Customers) })
        {
            Assert.Contains(
                "sales.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("customers", controller.GetMethod(nameof(DeliveryOrderTrackReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new DeliveryOrderTrackSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentNo", "documentDate", "contactName", "status", "challanType", "progress", "deliveredValue", "toBillValue"],
            source.Columns.Select(c => c.Key));
    }
}

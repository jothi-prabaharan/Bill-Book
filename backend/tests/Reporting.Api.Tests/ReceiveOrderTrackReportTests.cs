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
/// The Receive Order Track Report (TK-242), on receipts worked by hand. The branch's base currency is INR and the period
/// is October 2026. Widget A costs 400 and Gadget B 700:
/// <list type="bullet">
/// <item><b>GRN-9901</b> (Zenith, against POR-9911): Widget A, 10 delivered and 8 accepted at 4,000, worth 3,200, none billed;
/// and Gadget B, 5 at 3,500, 3 billed (2,100). <b>Partly billed</b>: 6,700 received, 2,100 billed, 4,600 to bill.</item>
/// <item><b>GRN-9902</b> (Acme, US dollars at 80): Widget A, 5 at USD 500 and a ring, 2 at USD 300, nothing billed.
/// <b>Not billed</b>: USD 800, so 64,000 in base, all of it to bill.</item>
/// <item><b>GRN-9903</b> (Zenith): Gadget B, 4 at 2,800, all billed. <b>Billed</b>.</item>
/// <item><b>GRN-9907</b> (Zenith): Gadget B, 3 delivered and 2 accepted at 1,000, worth 666.67, billed on two bills of 1
/// each. <b>Billed</b>.</item>
/// <item><b>GRN-9908</b> (Zenith): Widget A, 2 delivered and all of it rejected. <b>Nothing to bill</b>.</item>
/// <item><b>GRN-9909</b> (Zenith): Widget A, 5 at 2,000, billed in full, and a Gadget B line that was all rejected, which
/// owes nothing and does not keep the receipt from being <b>billed</b>.</item>
/// <item>A draft, a voided receipt and one dated in September, which are not in the list.</item>
/// </list>
/// The six receipts in the list come to 76,166.67 received, of which 7,566.67 is billed and 68,600.00 is still to bill.
/// Bills: posted ones for 3 of GRN-9901's Gadget B, 4 of GRN-9903's, one each of GRN-9907's twice and GRN-9909's 5; a voided
/// bill, a draft bill and a bill naming no receipt line bill nothing. The builders run on lists through the same expressions
/// the repository runs against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class ReceiveOrderTrackReportTests
{
    private const long Acme = 9201, Zenith = 9203;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static GoodsReceiptRead Receipt(
        long id, DateOnly date, long contact, string currency = "INR", decimal rate = 1m, long? order = null,
        DocumentStatus status = DocumentStatus.Posted, string? note = null) =>
        new()
        {
            GoodsReceiptId = id, TransactionTypeCode = "GRN", DocumentNo = $"GRN-{id}", DocumentDate = date, ContactId = contact, CurrencyCode = currency,
            ExchangeRate = rate, PurchaseOrderId = order, Status = status, VendorDeliveryNoteNo = note,
        };

    private static readonly List<GoodsReceiptRead> Receipts =
    [
        Receipt(9901, new(2026, 10, 5), Zenith, order: 9911, note: "DN-77"),
        Receipt(9902, new(2026, 10, 12), Acme, "USD", 80m),
        Receipt(9903, new(2026, 10, 20), Zenith),
        Receipt(9904, new(2026, 10, 22), Zenith, status: DocumentStatus.Draft),
        Receipt(9905, new(2026, 10, 23), Zenith, status: DocumentStatus.Void),
        Receipt(9906, new(2026, 9, 20), Zenith),
        Receipt(9907, new(2026, 10, 25), Zenith),
        Receipt(9908, new(2026, 10, 27), Zenith),
        Receipt(9909, new(2026, 10, 26), Zenith),
    ];

    private static GoodsReceiptDetailRead Line(long id, long receipt, int number, decimal delivered, decimal accepted, decimal taxable) =>
        new()
        {
            GoodsReceiptDetailId = id, GoodsReceiptId = receipt, LineNumber = number, Quantity = delivered, AcceptedQuantity = accepted,
            RejectedQuantity = delivered - accepted, TaxableAmount = taxable,
        };

    private static readonly List<GoodsReceiptDetailRead> Details =
    [
        Line(99011, 9901, 1, 10m, 8m, 4_000m),
        Line(99012, 9901, 2, 5m, 5m, 3_500m),
        Line(99021, 9902, 1, 5m, 5m, 500m),
        Line(99022, 9902, 2, 2m, 2m, 300m),
        Line(99031, 9903, 1, 4m, 4m, 2_800m),
        Line(99041, 9904, 1, 1m, 1m, 1_000m),
        Line(99051, 9905, 1, 1m, 1m, 1_000m),
        Line(99061, 9906, 1, 1m, 1m, 1_000m),
        Line(99071, 9907, 1, 3m, 2m, 1_000m),
        Line(99081, 9908, 1, 2m, 0m, 800m),
        Line(99091, 9909, 1, 5m, 5m, 2_000m),
        Line(99092, 9909, 2, 1m, 0m, 700m),
    ];

    private static BillRead Bill(long id, DocumentStatus status = DocumentStatus.Posted) =>
        new() { BillId = id, DocumentNo = $"BIL-{id}", ExchangeRate = 1m, Status = status };

    private static readonly List<BillRead> Bills =
    [
        Bill(9951), Bill(9952, DocumentStatus.Void), Bill(9953, DocumentStatus.Draft), Bill(9954), Bill(9955), Bill(9956), Bill(9957), Bill(9958),
    ];

    private static BillDetailRead BillLine(long id, long bill, long? receiptLine, decimal qty) =>
        new() { BillDetailId = id, BillId = bill, GoodsReceiptDetailId = receiptLine, Quantity = qty };

    private static readonly List<BillDetailRead> BillDetails =
    [
        BillLine(1, 9951, 99012, 3m),
        BillLine(2, 9952, 99012, 2m),
        BillLine(3, 9953, 99012, 1m),
        BillLine(4, 9954, 99031, 4m),
        BillLine(5, 9955, 99071, 1m),
        BillLine(6, 9956, 99071, 1m),
        BillLine(7, 9957, null, 9m),
        BillLine(8, 9958, 99091, 5m),
    ];

    private static readonly List<ReceiptOrderRef> Orders = [new() { PurchaseOrderId = 9911, DocumentNo = "POR-9911" }];

    private static readonly List<VendorRef> Vendors =
    [
        new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Components" },
        new() { ContactId = Acme, Code = "ACME", Name = "Acme Supplies" },
    ];

    private static ReceiveOrderTrackReportRequest Request(
        long? contact = null, GoodsReceiptBillingStatus? status = null, bool notYetBilled = false, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ContactId = contact, Status = status, NotYetBilledOnly = notYetBilled };

    private static IQueryable<ReceiveOrderBilledSum> BilledSums() =>
        ReceiveOrderTrackReportRepository.BilledSums(Bills.AsQueryable(), BillDetails.AsQueryable());

    private static List<ReceiveOrderTrackEntry> RowsFor(ReceiveOrderTrackReportRequest request)
    {
        IQueryable<GoodsReceiptRead> receipts = ReceiveOrderTrackReportRepository.Receipts(Receipts.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value);
        return [.. ReceiveOrderTrackReportRepository.Rows(
            receipts,
            ReceiveOrderTrackReportRepository.Figures(receipts, Details.AsQueryable()),
            ReceiveOrderTrackReportRepository.BilledFigures(receipts, Details.AsQueryable(), BilledSums()))];
    }

    private static ReceiveOrderTrackEntry RowOf(IEnumerable<ReceiveOrderTrackEntry> rows, long id) => rows.Single(r => r.GoodsReceiptId == id);

    // ---- Which receipts ---------------------------------------------------------------------

    [Fact]
    public void Posted_receipts_in_the_period_are_listed_and_a_draft_a_void_and_another_month_are_not()
    {
        Assert.Equal([9901L, 9902, 9903, 9907, 9908, 9909], RowsFor(Request()).Select(r => r.GoodsReceiptId).Order());
    }

    [Fact]
    public void The_vendor_narrows_the_receipts()
    {
        Assert.Equal([9902L], RowsFor(Request(contact: Acme)).Select(r => r.GoodsReceiptId));
        Assert.Equal([9901L, 9903, 9907, 9908, 9909], RowsFor(Request(contact: Zenith)).Select(r => r.GoodsReceiptId).Order());
    }

    // ---- What has been billed -----------------------------------------------------------------

    [Fact]
    public void A_receipt_line_is_billed_for_what_the_posted_bills_naming_it_add_up_to()
    {
        Dictionary<long, decimal> billed = BilledSums().ToDictionary(s => s.GoodsReceiptDetailId, s => s.Quantity);

        // A voided bill, a draft bill and a bill line naming no receipt line bill nothing; GRN-9907's two bills of 1 make 2.
        Assert.Equal([99012L, 99031, 99071, 99091], billed.Keys.Order());
        Assert.Equal((3m, 4m, 2m, 5m), (billed[99012], billed[99031], billed[99071], billed[99091]));
    }

    // ---- Status -------------------------------------------------------------------------------

    [Theory]
    [InlineData(9901L, GoodsReceiptBillingStatus.PartlyBilled)]
    [InlineData(9902L, GoodsReceiptBillingStatus.NotBilled)]
    [InlineData(9903L, GoodsReceiptBillingStatus.Billed)]
    [InlineData(9907L, GoodsReceiptBillingStatus.Billed)]
    [InlineData(9908L, GoodsReceiptBillingStatus.NothingToBill)]
    [InlineData(9909L, GoodsReceiptBillingStatus.Billed)]
    public void A_receipts_status_is_worked_out_from_every_line_with_goods_accepted(long id, GoodsReceiptBillingStatus expected)
    {
        Assert.Equal(expected, RowOf(RowsFor(Request()), id).Status);
    }

    [Fact]
    public void One_line_not_yet_billed_in_full_keeps_the_receipt_from_being_billed()
    {
        // GRN-9901 has a line billed in part and a line not billed at all: partly billed, not billed.
        Assert.Equal(GoodsReceiptBillingStatus.PartlyBilled, RowOf(RowsFor(Request()), 9901).Status);
    }

    [Fact]
    public void A_line_that_was_all_rejected_owes_nothing_and_does_not_keep_the_receipt_from_being_billed()
    {
        Assert.Equal(GoodsReceiptBillingStatus.Billed, RowOf(RowsFor(Request()), 9909).Status);
    }

    [Fact]
    public void A_status_chosen_lists_only_the_receipts_with_it_and_none_chosen_lists_all()
    {
        IQueryable<ReceiveOrderTrackEntry> rows = RowsFor(Request()).AsQueryable();

        Assert.Equal([9903L, 9907, 9909], ReceiveOrderTrackReportRepository.WithStatus(rows, GoodsReceiptBillingStatus.Billed).Select(r => r.GoodsReceiptId).Order());
        Assert.Equal([9908L], ReceiveOrderTrackReportRepository.WithStatus(rows, GoodsReceiptBillingStatus.NothingToBill).Select(r => r.GoodsReceiptId));
        Assert.Equal(6, ReceiveOrderTrackReportRepository.WithStatus(rows, null).Count());
    }

    [Fact]
    public void Not_yet_billed_only_keeps_the_receipts_with_something_still_to_bill()
    {
        List<ReceiveOrderTrackEntry> outstanding = [.. ReceiveOrderTrackReportRepository.NotYetBilledOnly(RowsFor(Request()).AsQueryable(), true)];

        Assert.Equal([9901L, 9902], outstanding.Select(r => r.GoodsReceiptId).Order());
        Assert.Equal(6, ReceiveOrderTrackReportRepository.NotYetBilledOnly(RowsFor(Request()).AsQueryable(), false).Count());
    }

    // ---- Values -------------------------------------------------------------------------------

    [Fact]
    public void A_receipts_value_is_what_its_accepted_goods_are_worth_at_the_receipts_own_cost()
    {
        ReceiveOrderTrackEntry partly = RowOf(RowsFor(Request()), 9901);

        // Widget A: 8 of 10 delivered at 4,000 is 3,200, not 4,000. Gadget B: 3,500.
        Assert.Equal((6_700m, 6_700m), (partly.Value, partly.ValueInCurrency));
    }

    [Fact]
    public void The_billed_value_is_the_billed_quantity_at_that_same_cost()
    {
        // 3 of 5 Gadget B at 3,500 is 2,100.
        Assert.Equal(2_100m, RowOf(RowsFor(Request()), 9901).BilledValue);
    }

    [Fact]
    public void A_value_that_does_not_divide_evenly_is_rounded_to_two_places_and_so_is_what_was_billed_of_it()
    {
        ReceiveOrderTrackEntry row = RowOf(RowsFor(Request()), 9907);

        // 2 of 3 delivered at 1,000 is 666.67, and all 2 accepted are billed.
        Assert.Equal((666.67m, 666.67m), (row.Value, row.BilledValue));
    }

    [Fact]
    public void A_receipt_in_another_currency_has_its_value_in_base_at_its_rate_and_its_own_beside_it()
    {
        ReceiveOrderTrackEntry row = RowOf(RowsFor(Request()), 9902);

        // USD 500 and USD 300 at 80.
        Assert.Equal((64_000m, 800m, 0m), (row.Value, row.ValueInCurrency, row.BilledValue));
    }

    [Fact]
    public void A_receipt_with_everything_rejected_is_worth_nothing()
    {
        ReceiveOrderTrackEntry row = RowOf(RowsFor(Request()), 9908);

        Assert.Equal((0m, 0m), (row.Value, row.BilledValue));
    }

    [Fact]
    public void The_default_list_adds_up_as_worked_by_hand()
    {
        List<ReceiveOrderTrackEntry> rows = RowsFor(Request());

        Assert.Equal((76_166.67m, 7_566.67m), (rows.Sum(r => r.Value), rows.Sum(r => r.BilledValue)));
    }

    // ---- Compose ------------------------------------------------------------------------------

    private static ReceiveOrderTrackReportResultDto Run(ReceiveOrderTrackReportRequest? request = null, IReadOnlyList<ReceiveOrderStatusCount>? counts = null)
    {
        request ??= Request();
        List<ReceiveOrderTrackEntry> rows = [.. ReceiveOrderTrackReportRepository.WithStatus(
            ReceiveOrderTrackReportRepository.NotYetBilledOnly(RowsFor(request).AsQueryable(), request.NotYetBilledOnly), request.Status)
            .OrderByDescending(r => r.DocumentDate).ThenByDescending(r => r.GoodsReceiptId)];
        var totals = new ReceiveOrderTrackEntry { Value = rows.Sum(r => r.Value), BilledValue = rows.Sum(r => r.BilledValue) };

        return ReceiveOrderTrackReportRepository.Compose(request, rows, rows.Count, Orders, Vendors, counts ?? [], totals, "INR");
    }

    private static ReceiveOrderTrackReceiptDto ReceiptDto(ReceiveOrderTrackReportResultDto report, long id) => report.Receipts.Single(r => r.GoodsReceiptId == id);

    [Fact]
    public void The_receipts_are_newest_first_with_their_vendor_order_and_delivery_note()
    {
        ReceiveOrderTrackReportResultDto report = Run();

        Assert.Equal([9908L, 9909, 9907, 9903, 9902, 9901], report.Receipts.Select(r => r.GoodsReceiptId));
        ReceiveOrderTrackReceiptDto receipt = ReceiptDto(report, 9901);
        Assert.Equal(("ZENITH", "Zenith Components", "POR-9911", "DN-77", GoodsReceiptBillingStatus.PartlyBilled), (receipt.ContactCode, receipt.ContactName, receipt.PurchaseOrderNo, receipt.VendorDeliveryNoteNo, receipt.Status));
        Assert.Null(ReceiptDto(report, 9902).PurchaseOrderNo);
    }

    [Fact]
    public void Received_less_billed_is_what_is_to_bill_on_every_receipt_and_in_total()
    {
        ReceiveOrderTrackReportResultDto report = Run();

        Assert.All(report.Receipts, r => Assert.Equal(r.Value - r.BilledValue, r.ToBillValue));
        Assert.Equal((4_600m, 64_000m, 0m), (ReceiptDto(report, 9901).ToBillValue, ReceiptDto(report, 9902).ToBillValue, ReceiptDto(report, 9903).ToBillValue));
        Assert.Equal(report.Totals.ToBillValue, report.Receipts.Sum(r => r.ToBillValue));
    }

    [Fact]
    public void The_totals_of_every_match_are_worked_out_by_hand()
    {
        ReceiveOrderTrackTotalsDto totals = Run().Totals;

        Assert.Equal((76_166.67m, 7_566.67m, 68_600m), (totals.Value, totals.BilledValue, totals.ToBillValue));
    }

    [Fact]
    public void A_foreign_receipt_carries_its_own_currency_and_value()
    {
        ReceiveOrderTrackReceiptDto receipt = ReceiptDto(Run(), 9902);

        Assert.Equal(("USD", 800m, 64_000m), (receipt.CurrencyCode, receipt.ValueInCurrency, receipt.Value));
    }

    [Fact]
    public void Every_status_has_a_count_a_nought_included()
    {
        ReceiveOrderTrackReportResultDto report = Run(counts: [new() { Status = GoodsReceiptBillingStatus.Billed, Count = 3 }]);

        Assert.Equal(Enum.GetValues<GoodsReceiptBillingStatus>().Length, report.StatusCounts.Count);
        Assert.Equal(3, report.StatusCounts[GoodsReceiptBillingStatus.Billed]);
        Assert.Equal(0, report.StatusCounts[GoodsReceiptBillingStatus.NotBilled]);
    }

    [Fact]
    public void A_vendor_the_names_do_not_know_is_still_listed_by_its_number()
    {
        ReceiveOrderTrackReportResultDto report = ReceiveOrderTrackReportRepository.Compose(
            Request(), [new() { GoodsReceiptId = 1, DocumentNo = "GRN-1", DocumentDate = October1, ContactId = 77, CurrencyCode = "INR" }], 1, [], [], [], new(), "INR");

        Assert.Equal("Vendor 77", report.Receipts[0].ContactName);
    }

    [Fact]
    public void The_period_and_page_are_passed_on()
    {
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        ReceiveOrderTrackReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 6), (report.PageNumber, report.PageSize, report.TotalReceipts));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(ReceiveOrderTrackReportRequest.FromDate), nameof(ReceiveOrderTrackReportRequest.ToDate)],
            typeof(ReceiveOrderTrackReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new ReceiveOrderTrackReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new ReceiveOrderTrackReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_and_an_unknown_status_are_refused_at_their_fields()
    {
        Assert.Equal(
            [(nameof(ReceiveOrderTrackReportRequest.ToDate), "To date cannot be before the from date.")],
            ReceiveOrderTrackReportRepository.Refusals(new ReceiveOrderTrackReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(ReceiveOrderTrackReportRequest.Status), "Status must be one of the options offered.")],
            ReceiveOrderTrackReportRepository.Refusals(Request(status: (GoodsReceiptBillingStatus)9)));

        Assert.Empty(ReceiveOrderTrackReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new ReceiveOrderTrackReportController(null!)
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
            new ReceiveOrderTrackReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_purchase_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(ReceiveOrderTrackReportController);

        Assert.Equal("api/statements/receive-order-track-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(ReceiveOrderTrackReportController.Run), nameof(ReceiveOrderTrackReportController.Vendors) })
        {
            Assert.Contains(
                "purchase.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("vendors", controller.GetMethod(nameof(ReceiveOrderTrackReportController.Vendors))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new ReceiveOrderTrackSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentNo", "documentDate", "contactName", "status", "value", "billedValue", "toBillValue", "goodsReceiptId"],
            source.Columns.Select(c => c.Key));
    }
}

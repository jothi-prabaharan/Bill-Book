using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Aged Payables Details (TK-234), on the books of the Aged Payables Summary tests, as at 31 October 2026: the same
/// open documents one at a time. Steelco has two (BIL-9501 of 20,000 less a 5,000 debit note, 15,000 open and 122 days past
/// due, in the last column; BIL-9503 of 10,000 less a 4,000 payment, 6,000 open and 6 days past due), Boltworks three (the
/// dollar bill BIL-9001 of 80,000 less 8,000 and 32,000 paid, 40,000 open and 51 days past due; BIL-9504 of 7,000 not yet due;
/// and the debit note DBN-9506 of 1,000 applied to nothing, negative in Current), and a manual journal of 2,500 belongs to no
/// vendor. A vendor's subtotal is the summary's figure for them and the totals are the 69,500 Accounts Payable stands at. The
/// documents come from the summary's builders on lists; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class AgedPayablesDetailsReportTests
{
    private const long Steelco = AgedPayablesSummaryReportTests.Steelco, Boltworks = AgedPayablesSummaryReportTests.Boltworks;

    private static readonly DateOnly October31 = AgedPayablesSummaryReportTests.October31;

    private static AgedPayablesDetailsReportRequest Request(
        DateOnly? asAt = null, AgeBasis? ageBy = null, int? bucket = null, long? contact = null) =>
        new() { AsAtDate = asAt ?? October31, AgeBy = ageBy, BucketDays = bucket, ContactId = contact };

    private static AgedPayablesDetailsReportResultDto Run(
        AgedPayablesDetailsReportRequest? request = null, IReadOnlyList<MoneyNumber>? numbers = null)
    {
        request ??= Request();
        return AgedPayablesDetailsReportRepository.Compose(
            request,
            AgedPayablesSummaryReportTests.OpenOn(request.AsAtDate!.Value, request.ContactId),
            AgedPayablesSummaryReportTests.Vendors(),
            numbers ?? [],
            "INR");
    }

    private static AgedPayablesSummaryReportResultDto SummaryFor(AgedPayablesDetailsReportRequest request) =>
        AgedPayablesSummaryReportRepository.Compose(
            new AgedPayablesSummaryReportRequest
            {
                AsAtDate = request.AsAtDate, AgeBy = request.AgeBy, BucketDays = request.BucketDays, ContactId = request.ContactId,
            },
            AgedPayablesSummaryReportTests.OpenOn(request.AsAtDate!.Value, request.ContactId),
            AgedPayablesSummaryReportTests.Vendors(),
            "INR");

    private static AgedPayablesDetailsVendorDto Vendor(AgedPayablesDetailsReportResultDto report, string name) =>
        report.Vendors.Single(v => v.ContactName == name);

    private static AgedPayablesDetailsDocumentDto Doc(AgedPayablesDetailsReportResultDto report, string number) =>
        report.Vendors.SelectMany(v => v.Documents).Single(d => d.DocumentNo == number);

    private static decimal[] Columns(AgedPayablesDetailsDocumentDto d) => [d.Current, d.Bucket1, d.Bucket2, d.Bucket3, d.Bucket4];

    // ---- Which documents, and in what order -------------------------------------------------

    [Fact]
    public void Every_document_still_open_is_listed_under_its_vendor_and_one_applied_in_full_is_not()
    {
        AgedPayablesDetailsReportResultDto report = Run();

        Assert.Equal(["BIL-9001", "BIL-9504", "DBN-9506"], Vendor(report, "Boltworks Ltd").Documents.Select(d => d.DocumentNo));
        Assert.Equal(["BIL-9501", "BIL-9503"], Vendor(report, "Steelco Supplies").Documents.Select(d => d.DocumentNo));
        Assert.DoesNotContain(report.Vendors.SelectMany(v => v.Documents), d => d.TypeCode == "SPM" || d.DocumentNo == "DBN-9502");
    }

    [Fact]
    public void Vendors_sort_by_name_and_what_no_vendor_claims_comes_last()
    {
        AgedPayablesDetailsReportResultDto report = Run();

        Assert.Equal(["Boltworks Ltd", "Steelco Supplies", AgedPayablesSummaryReportRepository.UnassignedName], report.Vendors.Select(v => v.ContactName));
        Assert.Null(report.Vendors[^1].ContactId);
        Assert.Equal("JRN-9507", Assert.Single(report.Vendors[^1].Documents).DocumentNo);
    }

    [Fact]
    public void A_vendors_documents_run_oldest_first()
    {
        AgedPayablesDetailsReportResultDto report = Run();

        Assert.Equal(report.Vendors.Select(v => v.Documents.Select(d => d.DocumentDate)), report.Vendors.Select(v => v.Documents.Select(d => d.DocumentDate).Order()));
    }

    [Fact]
    public void One_vendor_narrows_the_documents_to_theirs()
    {
        AgedPayablesDetailsReportResultDto report = Run(Request(contact: Steelco));

        Assert.Equal(["Steelco Supplies"], report.Vendors.Select(v => v.ContactName));
        Assert.Equal(21_000m, report.Totals.Outstanding);
    }

    // ---- A document -------------------------------------------------------------------------

    [Fact]
    public void A_document_carries_what_it_came_to_what_was_applied_and_what_is_left()
    {
        AgedPayablesDetailsReportResultDto report = Run();

        AgedPayablesDetailsDocumentDto steel = Doc(report, "BIL-9501");
        Assert.Equal((20_000m, 5_000m, 15_000m), (steel.Original, steel.Settled, steel.Outstanding));

        AgedPayablesDetailsDocumentDto dollars = Doc(report, "BIL-9001");
        Assert.Equal((80_000m, 40_000m, 40_000m), (dollars.Original, dollars.Settled, dollars.Outstanding));
    }

    [Fact]
    public void Each_documents_amount_sits_in_the_one_column_its_age_gives()
    {
        AgedPayablesDetailsReportResultDto report = Run();

        Assert.Equal([0m, 0m, 0m, 0m, 15_000m], Columns(Doc(report, "BIL-9501")));
        Assert.Equal([0m, 6_000m, 0m, 0m, 0m], Columns(Doc(report, "BIL-9503")));
        Assert.Equal([0m, 0m, 40_000m, 0m, 0m], Columns(Doc(report, "BIL-9001")));
        Assert.Equal([7_000m, 0m, 0m, 0m, 0m], Columns(Doc(report, "BIL-9504")));
        Assert.Equal([0m, 0m, 0m, 0m, 2_500m], Columns(Doc(report, "JRN-9507")));
    }

    [Fact]
    public void A_debit_note_applied_to_nothing_is_negative_and_in_current()
    {
        AgedPayablesDetailsDocumentDto debitNote = Doc(Run(), "DBN-9506");

        Assert.Equal((-1_000m, -1_000m), (debitNote.Outstanding, debitNote.Current));
        Assert.Equal((PayableDocumentKind.DebitNote, null), (debitNote.Kind, debitNote.DaysOverdue));
    }

    [Fact]
    public void Days_overdue_count_from_the_due_date_and_not_at_all_for_what_is_not_yet_due()
    {
        AgedPayablesDetailsReportResultDto report = Run();

        Assert.Equal(122, Doc(report, "BIL-9501").DaysOverdue);
        Assert.Equal(6, Doc(report, "BIL-9503").DaysOverdue);
        Assert.Equal(51, Doc(report, "BIL-9001").DaysOverdue);
        Assert.Null(Doc(report, "BIL-9504").DaysOverdue);
    }

    [Fact]
    public void Ageing_by_the_bill_date_counts_from_the_day_the_bill_was_dated()
    {
        AgedPayablesDetailsReportResultDto report = Run(Request(ageBy: AgeBasis.InvoiceDate, bucket: 15));

        Assert.Equal(82, Doc(report, "BIL-9001").DaysOverdue);
        Assert.Equal(11, Doc(report, "BIL-9504").DaysOverdue);
        Assert.Equal([0m, 7_000m, 0m, 0m, 0m], Columns(Doc(report, "BIL-9504")));
        Assert.Equal(["Current", "1-15 days", "16-30 days", "31-45 days", "Over 45 days"], report.BucketLabels);
    }

    [Fact]
    public void Only_a_bill_has_a_due_date_and_a_journal_reads_by_its_number_and_kind()
    {
        AgedPayablesDetailsReportResultDto report = Run();

        Assert.Equal(new DateOnly(2026, 7, 1), Doc(report, "BIL-9501").DueDate);
        Assert.Null(Doc(report, "DBN-9506").DueDate);
        Assert.Null(Doc(report, "JRN-9507").DueDate);
        Assert.Equal(PayableDocumentKind.Journal, Doc(report, "JRN-9507").Kind);
    }

    [Theory]
    [InlineData("BIL", PayableDocumentKind.Bill)]
    [InlineData("DBN", PayableDocumentKind.DebitNote)]
    [InlineData("SPM", PayableDocumentKind.Payment)]
    [InlineData("RCM", PayableDocumentKind.Receipt)]
    [InlineData("JRN", PayableDocumentKind.Journal)]
    [InlineData("OPB", PayableDocumentKind.OpeningBalance)]
    [InlineData("XYZ", PayableDocumentKind.Other)]
    public void A_documents_kind_is_told_from_the_code_its_postings_carry(string code, PayableDocumentKind expected)
    {
        Assert.Equal(expected, AgedPayablesDetailsReportRepository.KindOf(code));
    }

    [Fact]
    public void A_payment_takes_its_number_from_its_own_document_else_its_kind_and_id()
    {
        var open = new List<Reporting.Repository.Repositories.OpenReceivable>
        {
            new() { ContactId = Steelco, TypeCode = "SPM", DocumentId = 77, DocumentNo = null, DocumentDate = October31, DueDate = October31, Original = -50m, Amount = -50m },
            new() { ContactId = Steelco, TypeCode = "SPM", DocumentId = 78, DocumentNo = null, DocumentDate = October31, DueDate = October31, Original = -60m, Amount = -60m },
        };

        AgedPayablesDetailsReportResultDto report = AgedPayablesDetailsReportRepository.Compose(
            Request(), open, AgedPayablesSummaryReportTests.Vendors(), [new() { TypeCode = "SPM", DocumentId = 77, Number = "PAY-77" }], "INR");

        Assert.Equal(["PAY-77", "SPM-78"], report.Vendors.Single().Documents.Select(d => d.DocumentNo));
    }

    // ---- It agrees with the summary --------------------------------------------------------

    [Fact]
    public void A_vendors_subtotal_is_the_summarys_figure_for_that_vendor_in_every_column()
    {
        AgedPayablesDetailsReportRequest request = Request();
        AgedPayablesDetailsReportResultDto detail = Run(request);
        AgedPayablesSummaryReportResultDto summary = SummaryFor(request);

        foreach (AgedPayablesDetailsVendorDto vendor in detail.Vendors)
        {
            AgedPayablesSummaryRowDto row = summary.Vendors.Single(v => v.ContactId == vendor.ContactId);
            Assert.Equal(
                [row.Current, row.Bucket1, row.Bucket2, row.Bucket3, row.Bucket4, row.Total],
                [vendor.Subtotal.Current, vendor.Subtotal.Bucket1, vendor.Subtotal.Bucket2, vendor.Subtotal.Bucket3, vendor.Subtotal.Bucket4, vendor.Subtotal.Outstanding]);
        }
    }

    [Theory]
    [InlineData(2026, 10, 31, null, null)]
    [InlineData(2026, 10, 15, null, null)]
    [InlineData(2026, 9, 30, null, null)]
    [InlineData(2026, 10, 31, AgeBasis.InvoiceDate, 15)]
    [InlineData(2026, 10, 31, AgeBasis.InvoiceDate, 7)]
    public void The_totals_are_the_summarys_totals_and_the_payable_balance_whatever_the_date_or_ageing(int year, int month, int day, AgeBasis? ageBy, int? bucket)
    {
        AgedPayablesDetailsReportRequest request = Request(new DateOnly(year, month, day), ageBy, bucket);
        AgedPayablesDetailsReportResultDto detail = Run(request);
        AgedPayablesSummaryReportResultDto summary = SummaryFor(request);

        Assert.Equal(summary.Totals.Total, detail.Totals.Outstanding);
        Assert.Equal(
            [summary.Totals.Current, summary.Totals.Bucket1, summary.Totals.Bucket2, summary.Totals.Bucket3, summary.Totals.Bucket4],
            [detail.Totals.Current, detail.Totals.Bucket1, detail.Totals.Bucket2, detail.Totals.Bucket3, detail.Totals.Bucket4]);
    }

    [Fact]
    public void The_totals_add_up_to_the_payable_balance_and_each_documents_figures_add_up()
    {
        AgedPayablesDetailsReportResultDto report = Run();

        Assert.Equal(69_500m, report.Totals.Outstanding);
        Assert.Equal((118_500m, 49_000m), (report.Totals.Original, report.Totals.Settled));

        foreach (AgedPayablesDetailsDocumentDto document in report.Vendors.SelectMany(v => v.Documents))
        {
            Assert.Equal(document.Original - document.Settled, document.Outstanding);
            Assert.Equal(document.Outstanding, document.Current + document.Bucket1 + document.Bucket2 + document.Bucket3 + document.Bucket4);
        }
    }

    [Fact]
    public void The_vendors_msme_flag_and_the_settings_are_passed_on()
    {
        AgedPayablesDetailsReportResultDto report = Run(Request(ageBy: AgeBasis.InvoiceDate, bucket: 15));

        Assert.True(Vendor(report, "Steelco Supplies").IsMsme);
        Assert.False(Vendor(report, "Boltworks Ltd").IsMsme);
        Assert.Equal((October31, AgeBasis.InvoiceDate, 15, "INR"), (report.AsAtDate, report.AgeBy, report.BucketDays, report.BaseCurrencyCode));
    }

    [Fact]
    public void A_vendor_the_names_do_not_know_is_still_listed_by_its_number()
    {
        AgedPayablesDetailsReportResultDto report = AgedPayablesDetailsReportRepository.Compose(
            Request(),
            [new() { ContactId = 77, TypeCode = "BIL", DocumentId = 1, DocumentNo = "BIL-1", DocumentDate = October31, DueDate = October31, Original = 100m, Amount = 100m }],
            [], [], "INR");

        Assert.Equal("Vendor 77", report.Vendors.Single().ContactName);
    }

    // ---- The request and the guards ----------------------------------------------------------

    [Fact]
    public void Only_the_as_at_date_is_required()
    {
        Assert.Equal(
            [nameof(AgedPayablesDetailsReportRequest.AsAtDate)],
            typeof(AgedPayablesDetailsReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_as_at_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new AgedPayablesDetailsReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["As at date is required."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_bucket_width_or_age_basis_that_is_not_offered_is_refused_in_the_summarys_words()
    {
        Assert.Equal(
            [(nameof(AgedPayablesDetailsReportRequest.BucketDays), "Bucket size must be 7, 15 or 30 days.")],
            AgedPayablesDetailsReportRepository.Refusals(Request(bucket: 20)));

        Assert.Equal(
            [(nameof(AgedPayablesDetailsReportRequest.AgeBy), "Age by must be the due date or the bill date.")],
            AgedPayablesDetailsReportRepository.Refusals(Request(ageBy: (AgeBasis)9)));

        Assert.Empty(AgedPayablesDetailsReportRepository.Refusals(Request(bucket: 7)));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new AgedPayablesDetailsReportController(null!)
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
            new AgedPayablesDetailsReportRequest { AsAtDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_purchase_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(AgedPayablesDetailsReportController);

        Assert.Equal("api/statements/aged-payables-details", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(AgedPayablesDetailsReportController.Run), nameof(AgedPayablesDetailsReportController.Vendors) })
        {
            Assert.Contains(
                "purchase.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("vendors", controller.GetMethod(nameof(AgedPayablesDetailsReportController.Vendors))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same date ----------------------------------------------

    [Fact]
    public void The_catalog_source_declares_the_as_at_date_required_and_keeps_its_columns()
    {
        var source = new AgedPayablesDetailsSourceRepository();

        Assert.Equal(["asOf"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentNo", "documentDate", "dueDate", "contactName", "totalAmount", "amountDue", "billId"],
            source.Columns.Select(c => c.Key));
    }
}

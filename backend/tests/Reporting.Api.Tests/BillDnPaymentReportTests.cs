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
/// The Bill/DN Payment Report (TK-238), on vendor payments worked by hand. The branch's base currency is INR
/// and the period is October 2026:
/// <list type="bullet">
/// <item><b>SPM-9411</b> (10 October, Steelco, cash): 11,800, all of it settling BIL-9401.</item>
/// <item><b>SPM-9412</b> (12 October, Steelco, UPI): 10,000 settling BIL-9402.</item>
/// <item><b>SPM-9414</b> (20 October, Steelco, cheque): 3,000 of which 2,000 settles BIL-9403; the other
/// 1,000 was an advance and settles nothing.</item>
/// <item><b>SPM-9413</b> (22 October, Boltworks, transfer) in US dollars at 0.0125 a rupee: USD 100, which is 8,000
/// in base, settling BIL-9405.</item>
/// <item><b>SPM-9418</b> (15 October, Boltworks, UPI): 4,500 left the bank and 500 was withheld as TDS on an advance,
/// 5,000 settling an opening balance, which has no bill number.</item>
/// <item>Not paid to a vendor in the period: <b>SPM-9415</b> voided (its settlement voided with it), <b>SPM-9416</b>
/// dated 3 November, a draft, and <b>SPM-9419</b>, a TDS challan paying the government.</item>
/// </list>
/// What was paid comes to 37,300 in base, with 1,000 settling nothing. A debit note applied to BIL-9402 is not a payment's
/// settlement. The builders run on lists through the same expressions the repository runs against the read models; the
/// composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class BillDnPaymentReportTests
{
    private const long Boltworks = 9112, Steelco = 9111, Cash = 9301, Wallet = 9302;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static SpendMoneyRead Payment(
        long id, DateOnly date, long bank, long contact, decimal amount, string currency, decimal rate, PaymentMethod method,
        decimal tds = 0m, MoneyDocumentStatus status = MoneyDocumentStatus.Posted, bool challan = false) =>
        new()
        {
            SpendMoneyId = id, TransactionNo = status == MoneyDocumentStatus.Draft ? null : $"SPM-{id}", TransactionDate = date, BankAccountId = bank,
            ContactId = contact, Amount = amount, CurrencyCode = currency, ExchangeRate = rate, PaymentMethod = method, TdsAmount = tds, Status = status,
            IsTdsChallan = challan,
        };

    private static readonly List<SpendMoneyRead> Payments =
    [
        Payment(9411, new(2026, 10, 10), Cash, Steelco, 11_800m, "INR", 1m, PaymentMethod.Cash),
        Payment(9412, new(2026, 10, 12), Wallet, Steelco, 10_000m, "INR", 1m, PaymentMethod.Upi),
        Payment(9413, new(2026, 10, 22), Cash, Boltworks, 100m, "USD", 0.0125m, PaymentMethod.BankTransfer),
        Payment(9414, new(2026, 10, 20), Cash, Steelco, 3_000m, "INR", 1m, PaymentMethod.Cheque),
        Payment(9415, new(2026, 10, 30), Cash, Steelco, 9_440m, "INR", 1m, PaymentMethod.Cash, status: MoneyDocumentStatus.Void),
        Payment(9416, new(2026, 11, 3), Cash, Steelco, 9_440m, "INR", 1m, PaymentMethod.Cash),
        Payment(9417, new(2026, 10, 25), Cash, Steelco, 500m, "INR", 1m, PaymentMethod.Cash, status: MoneyDocumentStatus.Draft),
        Payment(9418, new(2026, 10, 15), Wallet, Boltworks, 4_500m, "INR", 1m, PaymentMethod.Upi, tds: 500m),
        Payment(9419, new(2026, 10, 28), Cash, Boltworks, 2_000m, "INR", 1m, PaymentMethod.BankTransfer, challan: true),
    ];

    private static long _id;

    private static TransactionRatioRead Ratio(string source, long sourceId, string target, long targetId, decimal amount, DateOnly on, bool voided = false) =>
        new()
        {
            TransactionRatioId = ++_id, SourceTransactionTypeCode = source, SourceTransactionId = sourceId,
            TargetTransactionTypeCode = target, TargetTransactionId = targetId, Amount = amount, AllocationDate = on, IsVoided = voided,
        };

    // On the payable side the bill is the credit-side document, so it is the source and the payment the target.
    private static readonly List<TransactionRatioRead> Ratios =
    [
        Ratio("BIL", 9401, "SPM", 9411, 11_800m, new(2026, 10, 10)),
        Ratio("BIL", 9402, "SPM", 9412, 10_000m, new(2026, 10, 12)),
        Ratio("BIL", 9403, "SPM", 9414, 2_000m, new(2026, 10, 20)),
        Ratio("BIL", 9405, "SPM", 9413, 100m, new(2026, 10, 22)),
        Ratio("OPB", 1, "SPM", 9418, 5_000m, new(2026, 10, 15)),
        // A debit note applied to a bill is not a payment's settlement.
        Ratio("BIL", 9402, "DBN", 9421, 1_180m, new(2026, 10, 26)),
        // The voided payment's settlement, voided with it, and the November payment's.
        Ratio("BIL", 9404, "SPM", 9415, 9_440m, new(2026, 10, 30), voided: true),
        Ratio("BIL", 9404, "SPM", 9416, 9_440m, new(2026, 11, 3)),
    ];

    private static BillRead Bill(long id, DateOnly date) =>
        new() { BillId = id, TransactionTypeCode = "BIL", DocumentNo = $"BIL-{id}", VendorBillNo = $"V/{id}", DocumentDate = date, Status = DocumentStatus.Posted };

    private static readonly List<BillRead> Bills =
    [
        Bill(9401, new(2026, 10, 2)), Bill(9402, new(2026, 10, 5)), Bill(9403, new(2026, 10, 10)), Bill(9405, new(2026, 10, 18)),
    ];

    private static readonly List<VendorRef> Vendors =
    [
        new() { ContactId = Steelco, Code = "STEELCO", Name = "Steelco Metals" },
        new() { ContactId = Boltworks, Code = "BOLTWORKS", Name = "Boltworks Imports" },
    ];

    private static readonly List<BankAccountRef> Banks =
    [
        new() { BankAccountId = Cash, Name = "Cash in hand" },
        new() { BankAccountId = Wallet, Name = "UPI wallet" },
    ];

    private static BillDnPaymentReportRequest Request(
        long? contact = null, long? bank = null, PaymentMethod? method = null, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ContactId = contact, BankAccountId = bank, PaymentMethod = method };

    private static List<PaymentRow> RowsFor(BillDnPaymentReportRequest request) =>
    [
        .. BillDnPaymentReportRepository.Rows(
            BillDnPaymentReportRepository.Payments(Payments.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value),
            Ratios.AsQueryable()),
    ];

    private static PaymentRow RowOf(IEnumerable<PaymentRow> rows, long id) => rows.Single(r => r.SpendMoneyId == id);

    // ---- Which payments ------------------------------------------------------------------

    [Fact]
    public void Posted_payments_dated_in_the_period_are_paid_and_a_void_a_draft_and_another_month_are_not()
    {
        List<PaymentRow> rows = RowsFor(Request());

        Assert.Equal([9411L, 9412, 9413, 9414, 9418], rows.Select(r => r.SpendMoneyId).Order());
    }

    [Fact]
    public void A_tds_challan_pays_the_government_and_is_not_a_vendor_payment()
    {
        // SPM-9419 is posted and dated in the period, but deposits deducted TDS.
        Assert.Contains(Payments, p => p.SpendMoneyId == 9419 && p.Status == MoneyDocumentStatus.Posted && p.IsTdsChallan);
        Assert.DoesNotContain(RowsFor(Request()), r => r.SpendMoneyId == 9419);
    }

    [Fact]
    public void The_period_includes_both_its_end_days()
    {
        Assert.Equal([9411L], RowsFor(Request(from: new(2026, 10, 10), to: new(2026, 10, 10))).Select(r => r.SpendMoneyId));
    }

    [Fact]
    public void The_vendor_bank_account_and_payment_method_each_narrow_the_payments()
    {
        Assert.Equal([9413L, 9418], RowsFor(Request(contact: Boltworks)).Select(r => r.SpendMoneyId).Order());
        Assert.Equal([9412L, 9418], RowsFor(Request(bank: Wallet)).Select(r => r.SpendMoneyId).Order());
        Assert.Equal([9414L], RowsFor(Request(method: PaymentMethod.Cheque)).Select(r => r.SpendMoneyId));
        Assert.Equal([9418L], RowsFor(Request(contact: Boltworks, bank: Wallet, method: PaymentMethod.Upi)).Select(r => r.SpendMoneyId));
    }

    // ---- What each payment settled ------------------------------------------------------------

    [Fact]
    public void A_payment_that_settled_all_of_it_has_nothing_unallocated()
    {
        PaymentRow row = RowOf(RowsFor(Request()), 9411);

        Assert.Equal((11_800m, 11_800m, 0m), (row.Amount, row.Settled, row.Unallocated));
    }

    [Fact]
    public void An_advance_on_a_payment_is_what_it_did_not_settle()
    {
        PaymentRow row = RowOf(RowsFor(Request()), 9414);

        Assert.Equal((3_000m, 2_000m, 1_000m), (row.Amount, row.Settled, row.Unallocated));
    }

    [Fact]
    public void Tds_withheld_is_settled_with_what_left_the_bank_so_the_payment_is_fully_allocated()
    {
        PaymentRow row = RowOf(RowsFor(Request()), 9418);

        // 4,500 left the bank and 500 was withheld, 5,000 settled.
        Assert.Equal((4_500m, 500m, 5_000m, 0m), (row.Amount, row.Tds, row.Settled, row.Unallocated));
    }

    [Fact]
    public void A_voided_settlement_and_a_debit_notes_are_not_a_payments_settlement()
    {
        List<PaymentRow> rows = RowsFor(Request(from: new(2026, 10, 1), to: new(2026, 11, 30)));

        // The voided payment is not paid, and the debit note applied to BIL-9402 is not on SPM-9412.
        Assert.DoesNotContain(rows, r => r.SpendMoneyId == 9415);
        Assert.Equal(10_000m, RowOf(rows, 9412).Settled);
    }

    [Fact]
    public void Allocated_and_unallocated_make_what_was_paid_with_the_tds_on_every_payment()
    {
        foreach (PaymentRow row in RowsFor(Request()))
        {
            Assert.Equal(row.Amount + row.Tds, row.Settled + row.Unallocated);
        }
    }

    // ---- Base currency ----------------------------------------------------------------

    [Fact]
    public void A_payment_in_another_currency_is_taken_to_base_at_its_rate_and_one_in_the_branchs_is_as_it_is()
    {
        List<PaymentRow> rows = RowsFor(Request());

        // USD 100 over 0.0125.
        Assert.Equal((100m, 8_000m), (RowOf(rows, 9413).Amount, RowOf(rows, 9413).AmountBase));
        Assert.Equal(11_800m, RowOf(rows, 9411).AmountBase);
    }

    [Fact]
    public void What_was_not_settled_is_also_taken_to_base_at_the_rate()
    {
        // USD 20 of an advance on the dollar payment would be 1,600.
        SpendMoneyRead[] payments = [Payment(9500, October1, Cash, Boltworks, 120m, "USD", 0.0125m, PaymentMethod.BankTransfer)];
        TransactionRatioRead[] ratios = [Ratio("BIL", 9405, "SPM", 9500, 100m, October1)];

        PaymentRow row = BillDnPaymentReportRepository.Rows(payments.AsQueryable(), ratios.AsQueryable()).Single();

        Assert.Equal((120m, 100m, 20m, 9_600m, 1_600m), (row.Amount, row.Settled, row.Unallocated, row.AmountBase, row.UnallocatedBase));
    }

    [Fact]
    public void The_total_paid_is_the_posted_payments_in_base_and_the_unallocated_is_what_settled_nothing()
    {
        List<PaymentRow> rows = RowsFor(Request());

        // 11,800 + 10,000 + 3,000 + 8,000 + 4,500.
        Assert.Equal(37_300m, rows.Sum(r => r.AmountBase));
        Assert.Equal(1_000m, rows.Sum(r => r.UnallocatedBase));
        Assert.Equal(Payments.Where(r => r.Status == MoneyDocumentStatus.Posted && r.TransactionDate <= October31).Sum(r => r.Amount * (r.ExchangeRate == 1m ? 1m : 80m)), rows.Sum(r => r.AmountBase));
    }

    // ---- By vendor ------------------------------------------------------------------

    [Fact]
    public void Vendors_are_totalled_largest_first()
    {
        List<VendorPaymentRow> totals = [.. BillDnPaymentReportRepository.VendorTotals(RowsFor(Request()).AsQueryable())];

        Assert.Equal([(Steelco, 3, 24_800m), (Boltworks, 2, 12_500m)], totals.Select(t => (t.ContactId, t.Payments, t.Paid)));
    }

    // ---- The documents of the payments ---------------------------------------------------------

    [Fact]
    public void The_documents_of_the_payments_asked_for_are_the_not_voided_settlements_naming_them_as_their_target()
    {
        List<PaymentSettlementRow> settled = [.. BillDnPaymentReportRepository.Settlements(Ratios.AsQueryable(), [9412L, 9415, 9416])];

        // 9415's was voided; the debit note's names a debit note as its target, not a payment.
        Assert.Equal([(9412L, "BIL", 9402L), (9416L, "BIL", 9404L)], settled.Select(s => (s.SpendMoneyId, s.TypeCode, s.DocumentId)).OrderBy(x => x.Item1));
    }

    [Fact]
    public void A_bills_number_vendor_number_and_date_are_looked_up_by_its_id()
    {
        List<BillRef> refs = [.. BillDnPaymentReportRepository.BillRefs(Bills.AsQueryable(), [9402L, 9405L])];

        Assert.Equal([(9402L, "BIL-9402", "V/9402"), (9405L, "BIL-9405", "V/9405")], refs.Select(r => (r.BillId, r.DocumentNo, r.VendorBillNo)).OrderBy(x => x.Item1));
    }

    // ---- Compose ----------------------------------------------------------------------

    private static BillDnPaymentReportResultDto Run(BillDnPaymentReportRequest? request = null, int vendorCount = 2)
    {
        request ??= Request();
        List<PaymentRow> rows = RowsFor(request);
        List<PaymentRow> page = [.. rows.OrderByDescending(r => r.PaymentDate).ThenByDescending(r => r.SpendMoneyId)];
        long[] ids = [.. page.Select(r => r.SpendMoneyId)];
        var totals = new BillDnPaymentTotalsDto { Payments = rows.Count, Paid = rows.Sum(r => r.AmountBase), Unallocated = rows.Sum(r => r.UnallocatedBase) };
        List<VendorPaymentRow> top = [.. BillDnPaymentReportRepository.VendorTotals(rows.AsQueryable())];

        return BillDnPaymentReportRepository.Compose(
            request, page, totals, top, vendorCount,
            [.. BillDnPaymentReportRepository.Settlements(Ratios.AsQueryable(), ids)],
            [.. BillDnPaymentReportRepository.BillRefs(Bills.AsQueryable(), [9401L, 9402L, 9403L, 9405L])],
            Vendors, Banks, "INR");
    }

    private static VendorPaymentDto Payment(BillDnPaymentReportResultDto report, long id) =>
        report.Payments.Single(r => r.SpendMoneyId == id);

    [Fact]
    public void The_payments_are_newest_first_with_their_vendors_bank_accounts_and_methods()
    {
        BillDnPaymentReportResultDto report = Run();

        Assert.Equal([9413L, 9414, 9418, 9412, 9411], report.Payments.Select(r => r.SpendMoneyId));
        VendorPaymentDto cheque = Payment(report, 9414);
        Assert.Equal(("SPM-9414", "Steelco Metals", "Cash in hand", PaymentMethod.Cheque), (cheque.PaymentNo, cheque.ContactName, cheque.BankAccountName, cheque.PaymentMethod));
    }

    [Fact]
    public void A_payment_lists_the_bills_it_settled_with_their_numbers_and_dates()
    {
        PaidDocumentDto document = Assert.Single(Payment(Run(), 9414).Documents);

        Assert.Equal(("BIL", "BIL-9403", "V/9403", new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 20), 2_000m),
            (document.TypeCode, document.DocumentNo, document.VendorBillNo, document.DocumentDate!.Value, document.AllocationDate, document.Amount));
    }

    [Fact]
    public void A_document_with_no_number_of_its_own_is_listed_by_its_code_and_id()
    {
        PaidDocumentDto opening = Assert.Single(Payment(Run(), 9418).Documents);

        Assert.Equal(("OPB", "OPB-1", null, null), (opening.TypeCode, opening.DocumentNo, opening.VendorBillNo, opening.DocumentDate));
    }

    [Fact]
    public void A_payment_with_an_advance_carries_what_it_settled_and_what_it_did_not()
    {
        VendorPaymentDto payment = Payment(Run(), 9414);

        Assert.Equal((3_000m, 2_000m, 1_000m), (payment.Amount, payment.Settled, payment.Unallocated));
    }

    [Fact]
    public void A_payment_in_another_currency_carries_its_currency_and_both_amounts()
    {
        VendorPaymentDto usd = Payment(Run(), 9413);

        Assert.Equal(("USD", 100m, 8_000m), (usd.CurrencyCode, usd.Amount, usd.AmountBase));
    }

    [Fact]
    public void A_payment_with_tds_withheld_carries_it()
    {
        Assert.Equal(500m, Payment(Run(), 9418).TdsAmount);
    }

    [Fact]
    public void A_vendor_or_account_the_names_do_not_know_is_still_listed_by_its_number()
    {
        PaymentRow[] page = [new() { SpendMoneyId = 1, PaymentNo = "SPM-1", PaymentDate = October1, ContactId = 77, BankAccountId = 88, CurrencyCode = "INR" }];

        BillDnPaymentReportResultDto report = BillDnPaymentReportRepository.Compose(
            Request(), page, new() { Payments = 1 }, [], 0, [], [], [], [], "INR");

        VendorPaymentDto payment = Assert.Single(report.Payments);
        Assert.Equal(("Vendor 77", "Account 88"), (payment.ContactName, payment.BankAccountName));
    }

    [Fact]
    public void The_totals_and_page_of_every_match_are_passed_on()
    {
        var request = Request();
        request.PageNumber = 2;
        request.PageSize = 10;

        BillDnPaymentReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((2, 10, 5), (report.PageNumber, report.PageSize, report.TotalPayments));
        Assert.Equal((5, 37_300m, 1_000m), (report.Totals.Payments, report.Totals.Paid, report.Totals.Unallocated));
    }

    [Fact]
    public void The_vendors_paid_most_are_listed_with_their_payments_and_what_they_were_paid()
    {
        BillDnPaymentReportResultDto report = Run();

        Assert.Equal(
            [("Steelco Metals", 3, 24_800m), ("Boltworks Imports", 2, 12_500m)],
            report.TopVendors.Select(c => (c.ContactName, c.Payments, c.Paid)));
        Assert.Equal((0, 0m), (report.OtherVendors, report.OtherVendorsPaid));
    }

    [Fact]
    public void Only_the_ten_largest_vendors_are_listed_and_the_rest_are_one_line()
    {
        PaymentRow[] page = [];
        VendorPaymentRow[] top = [.. Enumerable.Range(1, 11).Select(i => new VendorPaymentRow { ContactId = i, Payments = 1, Paid = 100m - i })];
        var totals = new BillDnPaymentTotalsDto { Payments = 13, Paid = top.Sum(t => t.Paid) + 40m };

        BillDnPaymentReportResultDto report = BillDnPaymentReportRepository.Compose(
            Request(), page, totals, top, vendorCount: 13, [], [], [], [], "INR");

        Assert.Equal(10, report.TopVendors.Count);
        // Vendors 11 (the one beyond the ten fetched) and two more, with 40 between them besides.
        Assert.Equal(3, report.OtherVendors);
        Assert.Equal(totals.Paid - report.TopVendors.Sum(c => c.Paid), report.OtherVendorsPaid);
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(BillDnPaymentReportRequest.FromDate), nameof(BillDnPaymentReportRequest.ToDate)],
            typeof(BillDnPaymentReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new BillDnPaymentReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new BillDnPaymentReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date_and_an_unknown_method_at_its_field()
    {
        Assert.Equal(
            [(nameof(BillDnPaymentReportRequest.ToDate), "To date cannot be before the from date.")],
            BillDnPaymentReportRepository.Refusals(new BillDnPaymentReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(BillDnPaymentReportRequest.PaymentMethod), "Payment method must be one of the methods offered.")],
            BillDnPaymentReportRepository.Refusals(Request(method: (PaymentMethod)99)));

        Assert.Empty(BillDnPaymentReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new BillDnPaymentReportController(null!)
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
            new BillDnPaymentReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_purchase_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(BillDnPaymentReportController);

        Assert.Equal("api/statements/bill-dn-payment-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[]
        {
            nameof(BillDnPaymentReportController.Run),
            nameof(BillDnPaymentReportController.Vendors),
            nameof(BillDnPaymentReportController.BankAccounts),
        })
        {
            Assert.Contains(
                "purchase.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("vendors", controller.GetMethod(nameof(BillDnPaymentReportController.Vendors))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("bank-accounts", controller.GetMethod(nameof(BillDnPaymentReportController.BankAccounts))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new BillDnPaymentSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(["spendMoneyNo", "paymentDate", "contactName", "documentNo", "amount", "spendMoneyId"], source.Columns.Select(c => c.Key));
    }
}

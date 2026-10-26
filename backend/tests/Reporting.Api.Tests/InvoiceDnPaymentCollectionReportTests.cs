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
/// The Invoice/DN Payment Collection Report (TK-227), on receipts worked by hand. The branch's base
/// currency is INR and the period is October 2026:
/// <list type="bullet">
/// <item><b>RCM-9411</b> (10 October, Zenith, cash): 11,800, all of it settling INV-9401.</item>
/// <item><b>RCM-9412</b> (12 October, Zenith, UPI): 10,000 settling INV-9402.</item>
/// <item><b>RCM-9414</b> (20 October, Zenith, cheque): 3,000 of which 2,000 settles INV-9403; the other
/// 1,000 was an advance and settles nothing.</item>
/// <item><b>RCM-9413</b> (22 October, Acme, transfer) in US dollars at 0.0125 a rupee: USD 100, which is 8,000
/// in base, settling INV-9405.</item>
/// <item><b>RCM-9418</b> (15 October, Acme, UPI): 4,500 reached the bank and 500 was withheld as TDS, 5,000
/// settling an opening balance, which has no invoice number.</item>
/// <item>Not collected in the period: <b>RCM-9415</b> voided (its settlement voided with it), <b>RCM-9416</b>
/// dated 3 November, and a draft.</item>
/// </list>
/// What was collected comes to 37,300 in base, with 1,000 settling nothing. The builders run on lists through
/// the same expressions the repository runs against the read models; the composition is pure. Nothing here
/// needs PostgreSQL.
/// </summary>
public sealed class InvoiceDnPaymentCollectionReportTests
{
    private const long Acme = 9101, Zenith = 9103, Cash = 9301, Wallet = 9302;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static ReceiveMoneyRead Receipt(
        long id, DateOnly date, long bank, long contact, decimal amount, string currency, decimal rate, PaymentMethod method,
        decimal tds = 0m, MoneyDocumentStatus status = MoneyDocumentStatus.Posted) =>
        new()
        {
            ReceiveMoneyId = id, TransactionNo = status == MoneyDocumentStatus.Draft ? null : $"RCM-{id}", TransactionDate = date, BankAccountId = bank,
            ContactId = contact, Amount = amount, CurrencyCode = currency, ExchangeRate = rate, PaymentMethod = method, TdsAmount = tds, Status = status,
        };

    private static readonly List<ReceiveMoneyRead> Receipts =
    [
        Receipt(9411, new(2026, 10, 10), Cash, Zenith, 11_800m, "INR", 1m, PaymentMethod.Cash),
        Receipt(9412, new(2026, 10, 12), Wallet, Zenith, 10_000m, "INR", 1m, PaymentMethod.Upi),
        Receipt(9413, new(2026, 10, 22), Cash, Acme, 100m, "USD", 0.0125m, PaymentMethod.BankTransfer),
        Receipt(9414, new(2026, 10, 20), Cash, Zenith, 3_000m, "INR", 1m, PaymentMethod.Cheque),
        Receipt(9415, new(2026, 10, 30), Cash, Zenith, 9_440m, "INR", 1m, PaymentMethod.Cash, status: MoneyDocumentStatus.Void),
        Receipt(9416, new(2026, 11, 3), Cash, Zenith, 9_440m, "INR", 1m, PaymentMethod.Cash),
        Receipt(9417, new(2026, 10, 25), Cash, Zenith, 500m, "INR", 1m, PaymentMethod.Cash, status: MoneyDocumentStatus.Draft),
        Receipt(9418, new(2026, 10, 15), Wallet, Acme, 4_500m, "INR", 1m, PaymentMethod.Upi, tds: 500m),
    ];

    private static long _id;

    private static TransactionRatioRead Ratio(string source, long sourceId, string target, long targetId, decimal amount, DateOnly on, bool voided = false) =>
        new()
        {
            TransactionRatioId = ++_id, SourceTransactionTypeCode = source, SourceTransactionId = sourceId,
            TargetTransactionTypeCode = target, TargetTransactionId = targetId, Amount = amount, AllocationDate = on, IsVoided = voided,
        };

    private static readonly List<TransactionRatioRead> Ratios =
    [
        Ratio("RCM", 9411, "INV", 9401, 11_800m, new(2026, 10, 10)),
        Ratio("RCM", 9412, "INV", 9402, 10_000m, new(2026, 10, 12)),
        Ratio("RCM", 9414, "INV", 9403, 2_000m, new(2026, 10, 20)),
        Ratio("RCM", 9413, "INV", 9405, 100m, new(2026, 10, 22)),
        Ratio("RCM", 9418, "OPB", 1, 5_000m, new(2026, 10, 15)),
        // A credit note applied to an invoice is not a receipt's settlement.
        Ratio("CRN", 9421, "INV", 9402, 1_180m, new(2026, 10, 26)),
        // The voided receipt's settlement, voided with it, and the November receipt's.
        Ratio("RCM", 9415, "INV", 9404, 9_440m, new(2026, 10, 30), voided: true),
        Ratio("RCM", 9416, "INV", 9404, 9_440m, new(2026, 11, 3)),
    ];

    private static InvoiceRead Invoice(long id, DateOnly date) =>
        new() { InvoiceId = id, TransactionTypeCode = "INV", DocumentNo = $"INV-{id}", DocumentDate = date, Status = DocumentStatus.Posted };

    private static readonly List<InvoiceRead> Invoices =
    [
        Invoice(9401, new(2026, 10, 2)), Invoice(9402, new(2026, 10, 5)), Invoice(9403, new(2026, 10, 10)), Invoice(9405, new(2026, 10, 18)),
    ];

    private static readonly List<CustomerRef> Customers =
    [
        new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Traders" },
        new() { ContactId = Acme, Code = "ACME", Name = "Acme Exports" },
    ];

    private static readonly List<BankAccountRef> Banks =
    [
        new() { BankAccountId = Cash, Name = "Cash in hand" },
        new() { BankAccountId = Wallet, Name = "UPI wallet" },
    ];

    private static InvoiceDnPaymentCollectionReportRequest Request(
        long? contact = null, long? bank = null, PaymentMethod? method = null, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ContactId = contact, BankAccountId = bank, PaymentMethod = method };

    private static List<CollectionRow> RowsFor(InvoiceDnPaymentCollectionReportRequest request) =>
    [
        .. InvoiceDnPaymentCollectionReportRepository.Rows(
            InvoiceDnPaymentCollectionReportRepository.Receipts(Receipts.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value),
            Ratios.AsQueryable()),
    ];

    private static CollectionRow RowOf(IEnumerable<CollectionRow> rows, long id) => rows.Single(r => r.ReceiveMoneyId == id);

    // ---- Which receipts ------------------------------------------------------------------

    [Fact]
    public void Posted_receipts_dated_in_the_period_are_collected_and_a_void_a_draft_and_another_month_are_not()
    {
        List<CollectionRow> rows = RowsFor(Request());

        Assert.Equal([9411L, 9412, 9413, 9414, 9418], rows.Select(r => r.ReceiveMoneyId).Order());
    }

    [Fact]
    public void The_period_includes_both_its_end_days()
    {
        Assert.Equal([9411L], RowsFor(Request(from: new(2026, 10, 10), to: new(2026, 10, 10))).Select(r => r.ReceiveMoneyId));
    }

    [Fact]
    public void The_customer_bank_account_and_payment_method_each_narrow_the_receipts()
    {
        Assert.Equal([9413L, 9418], RowsFor(Request(contact: Acme)).Select(r => r.ReceiveMoneyId).Order());
        Assert.Equal([9412L, 9418], RowsFor(Request(bank: Wallet)).Select(r => r.ReceiveMoneyId).Order());
        Assert.Equal([9414L], RowsFor(Request(method: PaymentMethod.Cheque)).Select(r => r.ReceiveMoneyId));
        Assert.Equal([9418L], RowsFor(Request(contact: Acme, bank: Wallet, method: PaymentMethod.Upi)).Select(r => r.ReceiveMoneyId));
    }

    // ---- What each receipt settled ------------------------------------------------------------

    [Fact]
    public void A_receipt_that_settled_all_of_it_has_nothing_unallocated()
    {
        CollectionRow row = RowOf(RowsFor(Request()), 9411);

        Assert.Equal((11_800m, 11_800m, 0m), (row.Amount, row.Settled, row.Unallocated));
    }

    [Fact]
    public void An_advance_on_a_receipt_is_what_it_did_not_settle()
    {
        CollectionRow row = RowOf(RowsFor(Request()), 9414);

        Assert.Equal((3_000m, 2_000m, 1_000m), (row.Amount, row.Settled, row.Unallocated));
    }

    [Fact]
    public void Tds_withheld_is_settled_with_what_reached_the_bank_so_the_receipt_is_fully_allocated()
    {
        CollectionRow row = RowOf(RowsFor(Request()), 9418);

        // 4,500 to the bank and 500 withheld, 5,000 settled.
        Assert.Equal((4_500m, 500m, 5_000m, 0m), (row.Amount, row.Tds, row.Settled, row.Unallocated));
    }

    [Fact]
    public void A_voided_settlement_and_a_credit_notes_are_not_a_receipts_settlement()
    {
        List<CollectionRow> rows = RowsFor(Request(from: new(2026, 10, 1), to: new(2026, 11, 30)));

        // The voided receipt is not collected, and the credit note against INV-9402 is not on RCM-9412.
        Assert.DoesNotContain(rows, r => r.ReceiveMoneyId == 9415);
        Assert.Equal(10_000m, RowOf(rows, 9412).Settled);
    }

    [Fact]
    public void Allocated_and_unallocated_make_what_was_received_with_the_tds_on_every_receipt()
    {
        foreach (CollectionRow row in RowsFor(Request()))
        {
            Assert.Equal(row.Amount + row.Tds, row.Settled + row.Unallocated);
        }
    }

    // ---- Base currency ----------------------------------------------------------------

    [Fact]
    public void A_receipt_in_another_currency_is_taken_to_base_at_its_rate_and_one_in_the_branchs_is_as_it_is()
    {
        List<CollectionRow> rows = RowsFor(Request());

        // USD 100 over 0.0125.
        Assert.Equal((100m, 8_000m), (RowOf(rows, 9413).Amount, RowOf(rows, 9413).AmountBase));
        Assert.Equal(11_800m, RowOf(rows, 9411).AmountBase);
    }

    [Fact]
    public void What_was_not_settled_is_also_taken_to_base_at_the_rate()
    {
        // USD 20 of an advance on the dollar receipt would be 1,600.
        ReceiveMoneyRead[] receipts = [Receipt(9500, October1, Cash, Acme, 120m, "USD", 0.0125m, PaymentMethod.BankTransfer)];
        TransactionRatioRead[] ratios = [Ratio("RCM", 9500, "INV", 9405, 100m, October1)];

        CollectionRow row = InvoiceDnPaymentCollectionReportRepository.Rows(receipts.AsQueryable(), ratios.AsQueryable()).Single();

        Assert.Equal((120m, 100m, 20m, 9_600m, 1_600m), (row.Amount, row.Settled, row.Unallocated, row.AmountBase, row.UnallocatedBase));
    }

    [Fact]
    public void The_total_collected_is_the_posted_receipts_in_base_and_the_unallocated_is_what_settled_nothing()
    {
        List<CollectionRow> rows = RowsFor(Request());

        // 11,800 + 10,000 + 3,000 + 8,000 + 4,500.
        Assert.Equal(37_300m, rows.Sum(r => r.AmountBase));
        Assert.Equal(1_000m, rows.Sum(r => r.UnallocatedBase));
        Assert.Equal(Receipts.Where(r => r.Status == MoneyDocumentStatus.Posted && r.TransactionDate <= October31).Sum(r => r.Amount * (r.ExchangeRate == 1m ? 1m : 80m)), rows.Sum(r => r.AmountBase));
    }

    // ---- By customer ------------------------------------------------------------------

    [Fact]
    public void Customers_are_totalled_largest_first()
    {
        List<CustomerCollectionRow> totals = [.. InvoiceDnPaymentCollectionReportRepository.CustomerTotals(RowsFor(Request()).AsQueryable())];

        Assert.Equal([(Zenith, 3, 24_800m), (Acme, 2, 12_500m)], totals.Select(t => (t.ContactId, t.Receipts, t.Collected)));
    }

    // ---- The documents of the receipts ---------------------------------------------------------

    [Fact]
    public void The_documents_of_the_receipts_asked_for_are_the_not_voided_settlements_naming_them_as_their_source()
    {
        List<SettlementRow> settled = [.. InvoiceDnPaymentCollectionReportRepository.Settlements(Ratios.AsQueryable(), [9412L, 9415, 9416])];

        // 9415's was voided; the credit note's has a different source.
        Assert.Equal([(9412L, "INV", 9402L), (9416L, "INV", 9404L)], settled.Select(s => (s.ReceiveMoneyId, s.TypeCode, s.DocumentId)).OrderBy(x => x.Item1));
    }

    [Fact]
    public void An_invoices_number_and_date_are_looked_up_by_its_id()
    {
        List<InvoiceRef> refs = [.. InvoiceDnPaymentCollectionReportRepository.InvoiceRefs(Invoices.AsQueryable(), [9402L, 9405L])];

        Assert.Equal([(9402L, "INV-9402"), (9405L, "INV-9405")], refs.Select(r => (r.InvoiceId, r.DocumentNo)).OrderBy(x => x.Item1));
    }

    // ---- Compose ----------------------------------------------------------------------

    private static InvoiceDnPaymentCollectionReportResultDto Run(InvoiceDnPaymentCollectionReportRequest? request = null, int customerCount = 2)
    {
        request ??= Request();
        List<CollectionRow> rows = RowsFor(request);
        List<CollectionRow> page = [.. rows.OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.ReceiveMoneyId)];
        long[] ids = [.. page.Select(r => r.ReceiveMoneyId)];
        var totals = new InvoiceDnPaymentCollectionTotalsDto { Receipts = rows.Count, Collected = rows.Sum(r => r.AmountBase), Unallocated = rows.Sum(r => r.UnallocatedBase) };
        List<CustomerCollectionRow> top = [.. InvoiceDnPaymentCollectionReportRepository.CustomerTotals(rows.AsQueryable())];

        return InvoiceDnPaymentCollectionReportRepository.Compose(
            request, page, totals, top, customerCount,
            [.. InvoiceDnPaymentCollectionReportRepository.Settlements(Ratios.AsQueryable(), ids)],
            [.. InvoiceDnPaymentCollectionReportRepository.InvoiceRefs(Invoices.AsQueryable(), [9401L, 9402L, 9403L, 9405L])],
            Customers, Banks, "INR");
    }

    private static ReceiptCollectionDto Receipt(InvoiceDnPaymentCollectionReportResultDto report, long id) =>
        report.Receipts.Single(r => r.ReceiveMoneyId == id);

    [Fact]
    public void The_receipts_are_newest_first_with_their_customers_bank_accounts_and_methods()
    {
        InvoiceDnPaymentCollectionReportResultDto report = Run();

        Assert.Equal([9413L, 9414, 9418, 9412, 9411], report.Receipts.Select(r => r.ReceiveMoneyId));
        ReceiptCollectionDto cheque = Receipt(report, 9414);
        Assert.Equal(("RCM-9414", "Zenith Traders", "Cash in hand", PaymentMethod.Cheque), (cheque.ReceiptNo, cheque.ContactName, cheque.BankAccountName, cheque.PaymentMethod));
    }

    [Fact]
    public void A_receipt_lists_the_invoices_it_settled_with_their_numbers_and_dates()
    {
        SettledDocumentDto document = Assert.Single(Receipt(Run(), 9414).Documents);

        Assert.Equal(("INV", "INV-9403", new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 20), 2_000m),
            (document.TypeCode, document.DocumentNo, document.DocumentDate!.Value, document.AllocationDate, document.Amount));
    }

    [Fact]
    public void A_document_with_no_number_of_its_own_is_listed_by_its_code_and_id()
    {
        SettledDocumentDto opening = Assert.Single(Receipt(Run(), 9418).Documents);

        Assert.Equal(("OPB", "OPB-1", null), (opening.TypeCode, opening.DocumentNo, opening.DocumentDate));
    }

    [Fact]
    public void A_receipt_with_an_advance_carries_what_it_settled_and_what_it_did_not()
    {
        ReceiptCollectionDto receipt = Receipt(Run(), 9414);

        Assert.Equal((3_000m, 2_000m, 1_000m), (receipt.Amount, receipt.Settled, receipt.Unallocated));
    }

    [Fact]
    public void A_receipt_in_another_currency_carries_its_currency_and_both_amounts()
    {
        ReceiptCollectionDto usd = Receipt(Run(), 9413);

        Assert.Equal(("USD", 100m, 8_000m), (usd.CurrencyCode, usd.Amount, usd.AmountBase));
    }

    [Fact]
    public void A_receipt_with_tds_withheld_carries_it()
    {
        Assert.Equal(500m, Receipt(Run(), 9418).TdsAmount);
    }

    [Fact]
    public void A_customer_or_account_the_names_do_not_know_is_still_listed_by_its_number()
    {
        CollectionRow[] page = [new() { ReceiveMoneyId = 1, ReceiptNo = "RCM-1", ReceiptDate = October1, ContactId = 77, BankAccountId = 88, CurrencyCode = "INR" }];

        InvoiceDnPaymentCollectionReportResultDto report = InvoiceDnPaymentCollectionReportRepository.Compose(
            Request(), page, new() { Receipts = 1 }, [], 0, [], [], [], [], "INR");

        ReceiptCollectionDto receipt = Assert.Single(report.Receipts);
        Assert.Equal(("Customer 77", "Account 88"), (receipt.ContactName, receipt.BankAccountName));
    }

    [Fact]
    public void The_totals_and_page_of_every_match_are_passed_on()
    {
        var request = Request();
        request.PageNumber = 2;
        request.PageSize = 10;

        InvoiceDnPaymentCollectionReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((2, 10, 5), (report.PageNumber, report.PageSize, report.TotalReceipts));
        Assert.Equal((5, 37_300m, 1_000m), (report.Totals.Receipts, report.Totals.Collected, report.Totals.Unallocated));
    }

    [Fact]
    public void The_customers_who_paid_most_are_listed_with_their_receipts_and_what_they_paid()
    {
        InvoiceDnPaymentCollectionReportResultDto report = Run();

        Assert.Equal(
            [("Zenith Traders", 3, 24_800m), ("Acme Exports", 2, 12_500m)],
            report.TopCustomers.Select(c => (c.ContactName, c.Receipts, c.Collected)));
        Assert.Equal((0, 0m), (report.OtherCustomers, report.OtherCustomersCollected));
    }

    [Fact]
    public void Only_the_ten_largest_customers_are_listed_and_the_rest_are_one_line()
    {
        CollectionRow[] page = [];
        CustomerCollectionRow[] top = [.. Enumerable.Range(1, 11).Select(i => new CustomerCollectionRow { ContactId = i, Receipts = 1, Collected = 100m - i })];
        var totals = new InvoiceDnPaymentCollectionTotalsDto { Receipts = 13, Collected = top.Sum(t => t.Collected) + 40m };

        InvoiceDnPaymentCollectionReportResultDto report = InvoiceDnPaymentCollectionReportRepository.Compose(
            Request(), page, totals, top, customerCount: 13, [], [], [], [], "INR");

        Assert.Equal(10, report.TopCustomers.Count);
        // Customers 11 (the one beyond the ten fetched) and two more, with 40 between them besides.
        Assert.Equal(3, report.OtherCustomers);
        Assert.Equal(totals.Collected - report.TopCustomers.Sum(c => c.Collected), report.OtherCustomersCollected);
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(InvoiceDnPaymentCollectionReportRequest.FromDate), nameof(InvoiceDnPaymentCollectionReportRequest.ToDate)],
            typeof(InvoiceDnPaymentCollectionReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new InvoiceDnPaymentCollectionReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new InvoiceDnPaymentCollectionReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date_and_an_unknown_method_at_its_field()
    {
        Assert.Equal(
            [(nameof(InvoiceDnPaymentCollectionReportRequest.ToDate), "To date cannot be before the from date.")],
            InvoiceDnPaymentCollectionReportRepository.Refusals(new InvoiceDnPaymentCollectionReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(InvoiceDnPaymentCollectionReportRequest.PaymentMethod), "Payment method must be one of the methods offered.")],
            InvoiceDnPaymentCollectionReportRepository.Refusals(Request(method: (PaymentMethod)99)));

        Assert.Empty(InvoiceDnPaymentCollectionReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new InvoiceDnPaymentCollectionReportController(null!)
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
            new InvoiceDnPaymentCollectionReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(InvoiceDnPaymentCollectionReportController);

        Assert.Equal("api/statements/invoice-dn-payment-collection-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[]
        {
            nameof(InvoiceDnPaymentCollectionReportController.Run),
            nameof(InvoiceDnPaymentCollectionReportController.Customers),
            nameof(InvoiceDnPaymentCollectionReportController.BankAccounts),
        })
        {
            Assert.Contains(
                "sales.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("customers", controller.GetMethod(nameof(InvoiceDnPaymentCollectionReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("bank-accounts", controller.GetMethod(nameof(InvoiceDnPaymentCollectionReportController.BankAccounts))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new InvoiceDnPaymentCollectionSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(["receiveMoneyNo", "receiptDate", "contactName", "documentNo", "amount"], source.Columns.Select(c => c.Key));
    }
}

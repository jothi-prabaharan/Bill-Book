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
/// The Aged Payables Summary (TK-233), on books worked by hand. The branch's base currency is INR and the report is as at
/// 31 October 2026:
/// <list type="bullet">
/// <item><b>Steelco Supplies</b> (an MSME): a bill of 20,000 on 1 June, due 1 July, less a debit note of 5,000 applied to it, so
/// 15,000 is open; a bill of 10,000 due 25 October with 4,000 paid on 28 October, so 6,000.</item>
/// <item><b>Boltworks Ltd</b>: a bill of 7,000 due 20 November; a debit note of 1,000 on 30 October applied to nothing; and a bill
/// of USD 1,000 at 80 on 10 August, due 10 September, paid in part by USD 100 (20 September) and USD 400 (12 October), so 40,000
/// is open (the payments go to base at the bill's rate, 8,000 and 32,000).</item>
/// <item>A <b>manual journal</b> of 2,500 to Accounts Payable on 1 August with no contact.</item>
/// <item>Settlements that must not count: one voided, and one effective on 6 November.</item>
/// </list>
/// By due date in 30-day columns Steelco is 0, 6,000, 0, 0 and 15,000 (21,000), Boltworks 6,000, 0, 40,000, 0 and 0 (46,000), and
/// the journal 2,500 in the last column; the totals are 6,000, 6,000, 40,000, 0 and 17,500, which is the 69,500 Accounts Payable
/// stands at. On 15 October it is 67,500 and on 30 September 89,500. The builders run on lists through the same expressions the
/// repository runs against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class AgedPayablesSummaryReportTests
{
    private const long Payable = 1, Inventory = 2, Returns = 3, Bank = 4, Receivable = 5;
    internal const long Steelco = 201, Boltworks = 202, IdleVendor = 203, CustomerOnly = 204;

    // The books below are shared with the Aged Payables Details tests, which read the same open documents.
    internal static readonly DateOnly October31 = new(2026, 10, 31);

    private static AccountRead Account(long id, string name, string? system = null) =>
        new() { AccountId = id, AccountCode = id.ToString(), AccountName = name, AccountSystemName = system, AccountTypeId = 1, IsActive = true };

    private static readonly List<AccountRead> Accounts =
    [
        Account(Payable, "Accounts Payable", ForeignCurrencyGainLossReportRepository.PayableAccountName),
        Account(Inventory, "Inventory"),
        Account(Returns, "Purchase Returns"),
        Account(Bank, "Bank"),
        Account(Receivable, "Accounts Receivable", ForeignCurrencyGainLossReportRepository.ReceivableAccountName),
    ];

    private static ContactRead Contact(long id, string code, string name, bool vendor = true, bool msme = false) =>
        new() { ContactId = id, ContactCode = code, DisplayName = name, IsVendor = vendor, IsCustomer = !vendor, IsMsme = msme, CurrencyCode = "INR", IsActive = true };

    internal static readonly List<ContactRead> Contacts =
    [
        Contact(Steelco, "STEELCO", "Steelco Supplies", msme: true),
        Contact(Boltworks, "BOLTWORKS", "Boltworks Ltd"),
        Contact(IdleVendor, "IDLEVEND", "Idle Vendor"),
        Contact(CustomerOnly, "CUST", "Customer Only", vendor: false),
    ];

    private static long _id;

    private static JournalLedgerRead Leg(
        string type, long transactionId, DateOnly on, long account, decimal debit, decimal credit, long? contact,
        decimal? sourceDebit = null, decimal? sourceCredit = null, string currency = "INR") =>
        new()
        {
            LedgerId = ++_id, LedgerDate = on, AccountId = account, TransactionTypeCode = type, TransactionId = transactionId,
            TransactionDetailId = 1, DebitAmountBase = debit, CreditAmountBase = credit,
            DebitAmount = sourceDebit ?? debit, CreditAmount = sourceCredit ?? credit, CurrencyCode = currency,
            ExchangeRate = 1m, ContactId = contact,
        };

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // Steelco, in base currency.
        Leg("BIL", 9501, new(2026, 6, 1), Inventory, 20_000m, 0m, Steelco), Leg("BIL", 9501, new(2026, 6, 1), Payable, 0m, 20_000m, Steelco),
        Leg("DBN", 9502, new(2026, 9, 15), Payable, 5_000m, 0m, Steelco), Leg("DBN", 9502, new(2026, 9, 15), Returns, 0m, 5_000m, Steelco),
        Leg("BIL", 9503, new(2026, 10, 10), Inventory, 10_000m, 0m, Steelco), Leg("BIL", 9503, new(2026, 10, 10), Payable, 0m, 10_000m, Steelco),
        Leg("SPM", 9505, new(2026, 10, 28), Payable, 4_000m, 0m, Steelco), Leg("SPM", 9505, new(2026, 10, 28), Bank, 0m, 4_000m, Steelco),

        // Boltworks: a bill of 7,000, a debit note applied to nothing, and a dollar bill at 80 with two dollar payments.
        Leg("BIL", 9504, new(2026, 10, 20), Inventory, 7_000m, 0m, Boltworks), Leg("BIL", 9504, new(2026, 10, 20), Payable, 0m, 7_000m, Boltworks),
        Leg("DBN", 9506, new(2026, 10, 30), Payable, 1_000m, 0m, Boltworks), Leg("DBN", 9506, new(2026, 10, 30), Returns, 0m, 1_000m, Boltworks),
        Leg("BIL", 9001, new(2026, 8, 10), Inventory, 80_000m, 0m, Boltworks, 1_000m, 0m, "USD"), Leg("BIL", 9001, new(2026, 8, 10), Payable, 0m, 80_000m, Boltworks, 0m, 1_000m, "USD"),
        Leg("SPM", 9009, new(2026, 9, 20), Payable, 8_000m, 0m, Boltworks, 100m, 0m, "USD"), Leg("SPM", 9009, new(2026, 9, 20), Bank, 0m, 8_000m, Boltworks),
        Leg("SPM", 9002, new(2026, 10, 12), Payable, 32_000m, 0m, Boltworks, 400m, 0m, "USD"), Leg("SPM", 9002, new(2026, 10, 12), Bank, 0m, 32_000m, Boltworks),

        // A manual journal to Accounts Payable with no contact.
        Leg("JRN", 9507, new(2026, 8, 1), Inventory, 2_500m, 0m, null), Leg("JRN", 9507, new(2026, 8, 1), Payable, 0m, 2_500m, null),

        // A customer's receivable leg, which no payable report counts.
        Leg("INV", 1, new(2026, 10, 1), Receivable, 999m, 0m, CustomerOnly),
    ];

    private static TransactionRatioRead Ratio(string source, long sourceId, string target, long targetId, decimal amount, DateOnly on, bool voided = false) =>
        new()
        {
            TransactionRatioId = ++_id, SourceTransactionTypeCode = source, SourceTransactionId = sourceId,
            TargetTransactionTypeCode = target, TargetTransactionId = targetId, Amount = amount, AllocationDate = on, IsVoided = voided,
        };

    private static readonly List<TransactionRatioRead> Ratios =
    [
        // The bill is the credit side and so the source; the debit note or payment settling it is the target.
        Ratio("BIL", 9501, "DBN", 9502, 5_000m, new(2026, 9, 15)),
        Ratio("BIL", 9503, "SPM", 9505, 4_000m, new(2026, 10, 28)),
        // Payments in their own currency: the bill's rate takes each line to base.
        Ratio("BIL", 9001, "SPM", 9009, 100m, new(2026, 9, 20)),
        Ratio("BIL", 9001, "SPM", 9002, 400m, new(2026, 10, 12)),
        // Neither of these counts on 31 October: one voided, one effective in November.
        Ratio("BIL", 9504, "SPM", 9511, 500m, new(2026, 10, 30), voided: true),
        Ratio("BIL", 9010, "SPM", 9511, 100m, new(2026, 11, 6)),
    ];

    private static BillRead Bill(long id, DateOnly date, DateOnly due, long contact) =>
        new() { BillId = id, TransactionTypeCode = "BIL", DocumentNo = $"BIL-{id}", DocumentDate = date, DueDate = due, ContactId = contact, CurrencyCode = "INR" };

    private static readonly List<BillRead> Bills =
    [
        Bill(9501, new(2026, 6, 1), new(2026, 7, 1), Steelco),
        Bill(9503, new(2026, 10, 10), new(2026, 10, 25), Steelco),
        Bill(9504, new(2026, 10, 20), new(2026, 11, 20), Boltworks),
        Bill(9001, new(2026, 8, 10), new(2026, 9, 10), Boltworks),
        Bill(9010, new(2026, 11, 5), new(2026, 12, 5), Boltworks),
    ];

    private static IQueryable<ReceivableDocument> DocumentsOn(DateOnly asAt, long? contact = null)
    {
        IQueryable<JournalLedgerRead> ledger = Ledger.AsQueryable();
        if (contact is long chosen)
        {
            ledger = ledger.Where(l => l.ContactId == chosen);
        }

        return AgedPayablesSummaryReportRepository.Documents(ledger, Accounts.AsQueryable(), asAt);
    }

    internal static List<OpenReceivable> OpenOn(DateOnly asAt, long? contact = null)
    {
        IQueryable<ReceivableDocument> documents = DocumentsOn(asAt, contact);
        return
        [
            .. AgedReceivablesSummaryReportRepository.OpenPayableDocuments(
                documents,
                AgedPayablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), documents, asAt),
                Bills.AsQueryable()),
        ];
    }

    internal static List<VendorRef> Vendors(bool everyVendor = false) =>
    [
        .. Contacts
            .Where(c => everyVendor ? c.IsVendor : c.ContactId is Steelco or Boltworks)
            .Select(c => new VendorRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName, IsMsme = c.IsMsme }),
    ];

    private static AgedPayablesSummaryReportRequest Request(
        DateOnly? asAt = null, AgeBasis? ageBy = null, int? bucket = null, long? contact = null, bool zero = false) =>
        new() { AsAtDate = asAt ?? October31, AgeBy = ageBy, BucketDays = bucket, ContactId = contact, ShowZeroBalances = zero };

    private static AgedPayablesSummaryReportResultDto Run(AgedPayablesSummaryReportRequest? request = null)
    {
        request ??= Request();
        return AgedPayablesSummaryReportRepository.Compose(
            request, OpenOn(request.AsAtDate!.Value, request.ContactId), Vendors(request.ShowZeroBalances), "INR");
    }

    private static AgedPayablesSummaryRowDto Row(AgedPayablesSummaryReportResultDto report, string name) =>
        report.Vendors.Single(v => v.ContactName == name);

    private static decimal[] Columns(AgedPayablesSummaryRowDto row) => [row.Current, row.Bucket1, row.Bucket2, row.Bucket3, row.Bucket4];

    // ---- Documents -----------------------------------------------------------------------

    [Fact]
    public void A_documents_payable_legs_net_credits_less_debits_so_what_is_owed_is_positive()
    {
        List<ReceivableDocument> documents = [.. DocumentsOn(October31)];

        Assert.Equal(20_000m, documents.Single(d => d.TypeCode == "BIL" && d.DocumentId == 9501).NetBase);
        Assert.Equal(-5_000m, documents.Single(d => d.TypeCode == "DBN" && d.DocumentId == 9502).NetBase);
        Assert.Equal(-4_000m, documents.Single(d => d.TypeCode == "SPM" && d.DocumentId == 9505).NetBase);
    }

    [Fact]
    public void A_dollar_bill_carries_its_total_in_both_currencies_and_only_payable_legs_count()
    {
        List<ReceivableDocument> documents = [.. DocumentsOn(October31)];
        ReceivableDocument bill = documents.Single(d => d.TypeCode == "BIL" && d.DocumentId == 9001);

        Assert.Equal((80_000m, 1_000m), (bill.NetBase, bill.NetSource));
        Assert.DoesNotContain(documents, d => d.TypeCode == "INV");
        Assert.Equal(new DateOnly(2026, 8, 10), bill.LedgerDate);
    }

    [Fact]
    public void Documents_after_the_date_are_not_there_yet()
    {
        Assert.DoesNotContain(DocumentsOn(new DateOnly(2026, 10, 15)), d => d.DocumentId is 9504 or 9506);
        Assert.Contains(DocumentsOn(October31), d => d.DocumentId == 9504);
    }

    // ---- Settlements -----------------------------------------------------------------------

    [Fact]
    public void A_payment_in_dollars_is_taken_to_base_at_the_rate_the_bill_was_booked_at()
    {
        List<ReceivableSettlement> settlements = [.. AgedPayablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), DocumentsOn(October31), October31)];

        Assert.Equal(8_000m, settlements.Single(s => s.TargetId == 9009).Amount);
        Assert.Equal(32_000m, settlements.Single(s => s.TargetId == 9002).Amount);
    }

    [Fact]
    public void A_settlement_in_base_currency_is_taken_as_it_is_and_runs_from_the_bill_to_the_payment()
    {
        List<ReceivableSettlement> settlements = [.. AgedPayablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), DocumentsOn(October31), October31)];
        ReceivableSettlement debitNote = settlements.Single(s => s.TargetId == 9502);

        Assert.Equal((5_000m, "BIL", 9501L, "DBN"), (debitNote.Amount, debitNote.SourceTypeCode, debitNote.SourceId, debitNote.TargetTypeCode));
        Assert.Equal(4_000m, settlements.Single(s => s.TargetId == 9505).Amount);
    }

    [Fact]
    public void A_voided_settlement_and_one_effective_after_the_date_hold_nothing()
    {
        List<ReceivableSettlement> settlements = [.. AgedPayablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), DocumentsOn(October31), October31)];

        Assert.DoesNotContain(settlements, s => s.TargetId == 9511);
        Assert.Equal(4, settlements.Count);
    }

    [Fact]
    public void A_settlement_effective_on_the_date_counts_and_one_the_day_after_does_not()
    {
        IQueryable<ReceivableDocument> documents = DocumentsOn(October31);

        List<ReceivableSettlement> onTheDay = [.. AgedPayablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), documents, new DateOnly(2026, 10, 28))];
        List<ReceivableSettlement> dayBefore = [.. AgedPayablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), documents, new DateOnly(2026, 10, 27))];

        Assert.Contains(onTheDay, s => s.TargetId == 9505);
        Assert.DoesNotContain(dayBefore, s => s.TargetId == 9505);
    }

    // ---- Open documents ----------------------------------------------------------------------

    [Fact]
    public void A_bill_less_its_settlements_is_what_is_open_and_a_document_that_nets_to_nothing_is_gone()
    {
        List<OpenReceivable> open = OpenOn(October31);

        Assert.Equal(15_000m, open.Single(o => o is { TypeCode: "BIL", DocumentId: 9501 }).Amount);
        Assert.Equal(6_000m, open.Single(o => o is { TypeCode: "BIL", DocumentId: 9503 }).Amount);
        Assert.Equal(7_000m, open.Single(o => o is { TypeCode: "BIL", DocumentId: 9504 }).Amount);
        Assert.Equal(40_000m, open.Single(o => o is { TypeCode: "BIL", DocumentId: 9001 }).Amount);
        Assert.DoesNotContain(open, o => o.TypeCode == "DBN" && o.DocumentId == 9502);
        Assert.DoesNotContain(open, o => o.TypeCode == "SPM");
    }

    [Fact]
    public void A_debit_note_applied_to_nothing_is_open_and_negative()
    {
        OpenReceivable debitNote = OpenOn(October31).Single(o => o is { TypeCode: "DBN", DocumentId: 9506 });

        Assert.Equal(-1_000m, debitNote.Amount);
    }

    [Fact]
    public void A_bill_brings_its_own_bill_and_due_dates_and_another_document_its_ledger_date()
    {
        List<OpenReceivable> open = OpenOn(October31);
        OpenReceivable bill = open.Single(o => o is { TypeCode: "BIL", DocumentId: 9501 });
        OpenReceivable journal = open.Single(o => o.TypeCode == "JRN");

        Assert.Equal((new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1), "BIL-9501"), (bill.DocumentDate, bill.DueDate, bill.DocumentNo));
        Assert.Equal((new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 1), 2_500m), (journal.DocumentDate, journal.DueDate, journal.Amount));
    }

    [Fact]
    public void The_open_documents_add_up_to_the_payable_accounts_balance_on_each_date()
    {
        Assert.Equal(69_500m, OpenOn(October31).Sum(o => o.Amount));
        Assert.Equal(67_500m, OpenOn(new DateOnly(2026, 10, 15)).Sum(o => o.Amount));
        Assert.Equal(89_500m, OpenOn(new DateOnly(2026, 9, 30)).Sum(o => o.Amount));
    }

    [Fact]
    public void A_settlement_against_a_document_the_report_does_not_hold_is_none_of_its_business()
    {
        // BIL 9010 is after the date: its settlement must not turn up as a negative anywhere.
        Assert.DoesNotContain(OpenOn(October31), o => o.DocumentId == 9010);
    }

    // ---- The columns --------------------------------------------------------------------------

    [Fact]
    public void By_due_date_in_thirty_day_columns_each_vendor_falls_where_worked_out_by_hand()
    {
        AgedPayablesSummaryReportResultDto report = Run();

        Assert.Equal([0m, 6_000m, 0m, 0m, 15_000m], Columns(Row(report, "Steelco Supplies")));
        Assert.Equal([6_000m, 0m, 40_000m, 0m, 0m], Columns(Row(report, "Boltworks Ltd")));
        Assert.Equal([0m, 0m, 0m, 0m, 2_500m], Columns(Row(report, AgedPayablesSummaryReportRepository.UnassignedName)));
        Assert.Equal([6_000m, 6_000m, 40_000m, 0m, 17_500m], Columns(report.Totals));
        Assert.Equal((21_000m, 46_000m, 2_500m, 69_500m), (Row(report, "Steelco Supplies").Total, Row(report, "Boltworks Ltd").Total, Row(report, AgedPayablesSummaryReportRepository.UnassignedName).Total, report.Totals.Total));
    }

    [Fact]
    public void The_columns_on_earlier_dates_are_the_hand_worked_ones()
    {
        AgedPayablesSummaryReportResultDto fifteenth = Run(Request(new DateOnly(2026, 10, 15)));
        Assert.Equal([10_000m, 0m, 40_000m, 2_500m, 15_000m], Columns(fifteenth.Totals));

        AgedPayablesSummaryReportResultDto thirtieth = Run(Request(new DateOnly(2026, 9, 30)));
        Assert.Equal([0m, 72_000m, 2_500m, 0m, 15_000m], Columns(thirtieth.Totals));
    }

    [Fact]
    public void A_debit_note_or_payment_not_yet_applied_goes_in_current_whatever_its_age()
    {
        AgedPayablesSummaryReportResultDto report = Run();

        Assert.Equal(6_000m, Row(report, "Boltworks Ltd").Current);
    }

    [Fact]
    public void Ageing_by_the_bill_date_in_fifteen_day_columns_moves_what_it_should()
    {
        AgedPayablesSummaryReportResultDto report = Run(Request(ageBy: AgeBasis.InvoiceDate, bucket: 15));

        Assert.Equal(["Current", "1-15 days", "16-30 days", "31-45 days", "Over 45 days"], report.BucketLabels);
        Assert.Equal([-1_000m, 7_000m, 0m, 0m, 40_000m], Columns(Row(report, "Boltworks Ltd")));
        Assert.Equal([0m, 0m, 6_000m, 0m, 15_000m], Columns(Row(report, "Steelco Supplies")));
        Assert.Equal(69_500m, report.Totals.Total);
    }

    [Fact]
    public void Defaults_are_the_due_date_and_thirty_days()
    {
        Assert.Equal((AgeBasis.DueDate, 30), AgedPayablesSummaryReportRepository.Defaults(Request()));
        Assert.Equal((AgeBasis.InvoiceDate, 7), AgedPayablesSummaryReportRepository.Defaults(Request(ageBy: AgeBasis.InvoiceDate, bucket: 7)));
    }

    // ---- The rows --------------------------------------------------------------------------------

    [Fact]
    public void A_vendor_with_nothing_owed_is_listed_only_when_asked()
    {
        Assert.DoesNotContain(Run().Vendors, v => v.ContactName == "Idle Vendor");

        AgedPayablesSummaryReportResultDto withZero = Run(Request(zero: true));
        Assert.Equal(0m, Row(withZero, "Idle Vendor").Total);
        Assert.DoesNotContain(withZero.Vendors, v => v.ContactName == "Customer Only");
    }

    [Fact]
    public void Vendors_sort_by_name_and_what_no_vendor_claims_comes_last()
    {
        AgedPayablesSummaryReportResultDto report = Run(Request(zero: true));

        Assert.Equal(["Boltworks Ltd", "Idle Vendor", "Steelco Supplies", AgedPayablesSummaryReportRepository.UnassignedName], report.Vendors.Select(v => v.ContactName));
        Assert.Null(report.Vendors[^1].ContactId);
    }

    [Fact]
    public void An_msme_vendor_is_flagged_and_no_other_row_is()
    {
        AgedPayablesSummaryReportResultDto report = Run(Request(zero: true));

        Assert.True(Row(report, "Steelco Supplies").IsMsme);
        Assert.False(Row(report, "Boltworks Ltd").IsMsme);
        Assert.False(Row(report, AgedPayablesSummaryReportRepository.UnassignedName).IsMsme);
        Assert.False(report.Totals.IsMsme);
    }

    [Fact]
    public void A_vendor_the_names_do_not_know_is_still_listed_by_its_number()
    {
        AgedPayablesSummaryReportResultDto report = AgedPayablesSummaryReportRepository.Compose(
            Request(), [new() { ContactId = 77, Amount = 100m, DocumentDate = October31, DueDate = October31 }], [], "INR");

        Assert.Equal("Vendor 77", report.Vendors.Single().ContactName);
    }

    [Fact]
    public void One_vendor_narrows_the_open_documents_to_theirs()
    {
        AgedPayablesSummaryReportResultDto report = Run(Request(contact: Steelco));

        Assert.Equal(["Steelco Supplies"], report.Vendors.Select(v => v.ContactName));
        Assert.Equal(21_000m, report.Totals.Total);
    }

    [Fact]
    public void The_as_at_date_and_settings_are_passed_on()
    {
        AgedPayablesSummaryReportResultDto report = Run(Request(ageBy: AgeBasis.InvoiceDate, bucket: 15));

        Assert.Equal((October31, AgeBasis.InvoiceDate, 15, "INR"), (report.AsAtDate, report.AgeBy, report.BucketDays, report.BaseCurrencyCode));
    }

    // ---- The request and the guards ----------------------------------------------------------------

    [Fact]
    public void Only_the_as_at_date_is_required()
    {
        Assert.Equal(
            [nameof(AgedPayablesSummaryReportRequest.AsAtDate)],
            typeof(AgedPayablesSummaryReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_as_at_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new AgedPayablesSummaryReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["As at date is required."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_bucket_width_or_age_basis_that_is_not_offered_is_refused_at_its_field()
    {
        Assert.Equal(
            [(nameof(AgedPayablesSummaryReportRequest.BucketDays), "Bucket size must be 7, 15 or 30 days.")],
            AgedPayablesSummaryReportRepository.Refusals(Request(bucket: 20)));

        Assert.Equal(
            [(nameof(AgedPayablesSummaryReportRequest.AgeBy), "Age by must be the due date or the bill date.")],
            AgedPayablesSummaryReportRepository.Refusals(Request(ageBy: (AgeBasis)9)));

        Assert.Empty(AgedPayablesSummaryReportRepository.Refusals(Request(bucket: 15)));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new AgedPayablesSummaryReportController(null!)
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
            new AgedPayablesSummaryReportRequest { AsAtDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_purchase_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(AgedPayablesSummaryReportController);

        Assert.Equal("api/statements/aged-payables-summary", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(AgedPayablesSummaryReportController.Run), nameof(AgedPayablesSummaryReportController.Vendors) })
        {
            Assert.Contains(
                "purchase.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("vendors", controller.GetMethod(nameof(AgedPayablesSummaryReportController.Vendors))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    [Fact]
    public void The_vendor_filter_offers_vendors_by_name_and_not_customers()
    {
        List<AgedPayablesVendorChoiceDto> choices = [.. AgedPayablesSummaryReportRepository.VendorChoices(Contacts.AsQueryable())];

        Assert.Equal(["Boltworks Ltd", "Idle Vendor", "Steelco Supplies"], choices.Select(c => c.ContactName));
        Assert.DoesNotContain(choices, c => c.ContactId == CustomerOnly);
    }

    // ---- The catalog source, held to the same date ----------------------------------------------------

    [Fact]
    public void The_catalog_source_declares_the_as_at_date_required_and_keeps_its_columns()
    {
        var source = new ApAgingSummarySourceRepository();

        Assert.Equal(["asOf"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["contactCode", "contactName", "current", "days1_30", "days31_60", "days61_90", "days90Plus", "total", "contactId"],
            source.Columns.Select(c => c.Key));
    }
}

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
/// The Reconciliation Report (TK-202), on one bank account's half-year worked
/// by hand: money documents in the books, a statement imported with running
/// balances, and lines matched, ignored and left unmatched. On 30 September
/// the account reconciles with a cheque not yet cleared, a receipt not yet
/// credited, interest the books do not have and a debit booked after the day.
///
/// The queries run on lists through the same expressions the repository runs
/// against the read models; the composition is pure.
/// </summary>
public sealed class BankReconciliationReportTests
{
    private static readonly DateOnly AsOf = new(2026, 9, 30);

    private const long Main = 7, PettyCash = 8, MainLedger = 9001;

    private static readonly BankAccountRead MainBank = new()
    {
        BankAccountId = Main, LedgerAccountId = MainLedger, AccountName = "Main Bank",
        AccountNumber = "0012345678", CurrencyCode = "INR", IsActive = true,
    };

    private static readonly List<BankAccountRead> Banks =
    [
        MainBank,
        new() { BankAccountId = PettyCash, LedgerAccountId = 9002, AccountName = "Petty Cash", AccountNumber = "-", CurrencyCode = "INR", IsActive = true },
    ];

    private static readonly List<ContactRead> Contacts =
    [
        new() { ContactId = 1, ContactCode = "C1", DisplayName = "Acme Traders" },
        new() { ContactId = 2, ContactCode = "V1", DisplayName = "Rent Co" },
    ];

    private static SpendMoneyRead Spend(long id, DateOnly date, decimal amount, long contact = 2, MoneyDocumentStatus status = MoneyDocumentStatus.Posted, long bank = Main, string? memo = null) =>
        new()
        {
            SpendMoneyId = id, TransactionNo = $"SPM-{id}", TransactionDate = date, BankAccountId = bank, ContactId = contact,
            Amount = amount, CurrencyCode = "INR", ExchangeRate = 1m, ReferenceNo = $"CHQ{id}", Status = status, Memo = memo,
        };

    private static ReceiveMoneyRead Receive(long id, DateOnly date, decimal amount, long contact = 1, MoneyDocumentStatus status = MoneyDocumentStatus.Posted) =>
        new()
        {
            ReceiveMoneyId = id, TransactionNo = $"RCM-{id}", TransactionDate = date, BankAccountId = Main, ContactId = contact,
            Amount = amount, CurrencyCode = "INR", ExchangeRate = 1m, ReferenceNo = $"R{id}", Status = status,
        };

    private static TransferMoneyRead Transfer(long id, DateOnly date, long from, long to, decimal amount) =>
        new()
        {
            TransferMoneyId = id, TransactionNo = $"TRM-{id}", TransactionDate = date, FromBankAccountId = from,
            ToBankAccountId = to, Amount = amount, CurrencyCode = "INR", ExchangeRate = 1m, Status = MoneyDocumentStatus.Posted,
        };

    private static readonly List<SpendMoneyRead> SpendMoney =
    [
        Spend(2, new(2026, 5, 10), 5_000m),                       // rent, cleared 12 May
        Spend(3, new(2026, 9, 28), 3_000m, contact: 99, memo: "Cheque to plumber"), // not yet cleared
        Spend(7, new(2026, 10, 2), 1_000m),                       // booked after the day the bank took it
        Spend(9, new(2026, 6, 15), 750m, status: MoneyDocumentStatus.Void),         // voided: not in the books
        Spend(10, new(2026, 6, 16), 800m, status: MoneyDocumentStatus.Draft),         // a draft: not in the books
        Spend(11, new(2026, 6, 17), 900m, bank: PettyCash),       // another account's
    ];

    private static readonly List<ReceiveMoneyRead> ReceiveMoney =
    [
        Receive(1, new(2026, 5, 1), 20_000m),   // credited 2 May
        Receive(4, new(2026, 9, 29), 7_000m),   // credited 3 October
    ];

    private static readonly List<TransferMoneyRead> Transfers =
    [
        Transfer(5, new(2026, 6, 1), Main, PettyCash, 10_000m),
        Transfer(6, new(2026, 7, 1), PettyCash, Main, 2_500m),
        Transfer(12, new(2026, 7, 5), PettyCash, 99, 400m),       // between two other accounts
    ];

    private static JournalLedgerRead Leg(DateOnly date, decimal debit, decimal credit, string currency = "INR", decimal? debitBase = null, decimal? creditBase = null, long account = MainLedger) =>
        new()
        {
            LedgerDate = date, AccountId = account, CurrencyCode = currency,
            DebitAmount = debit, CreditAmount = credit,
            DebitAmountBase = debitBase ?? debit, CreditAmountBase = creditBase ?? credit,
        };

    private static readonly List<JournalLedgerRead> Ledger =
    [
        Leg(new(2026, 4, 1), 100_000m, 0m),  // opening balance
        Leg(new(2026, 5, 1), 20_000m, 0m),   // RCM-1
        Leg(new(2026, 5, 10), 0m, 5_000m),   // SPM-2
        Leg(new(2026, 6, 1), 0m, 10_000m),   // TRM-5 out
        Leg(new(2026, 6, 30), 0m, 200m),     // bank charges by journal
        Leg(new(2026, 7, 1), 2_500m, 0m),    // TRM-6 in
        Leg(new(2026, 9, 28), 0m, 3_000m),   // SPM-3
        Leg(new(2026, 9, 29), 7_000m, 0m),   // RCM-4
        Leg(new(2026, 10, 2), 0m, 1_000m),   // SPM-7, after the day
        Leg(new(2026, 5, 1), 999m, 0m, account: 9002), // another account
    ];

    private static ReconciliationStatementLine Line(
        long id, DateOnly date, decimal deposit, decimal withdrawal, decimal? running,
        StatementLineStatus status, string? code = null, long? doc = null, long statement = 1) =>
        new()
        {
            BankStatementLineId = id, BankStatementId = statement, LineNumber = (int)id, TransactionDate = date,
            Description = $"Line {id}", ReferenceNo = $"REF{id}", DepositAmount = deposit, WithdrawalAmount = withdrawal,
            RunningBalance = running, Status = status, MatchedTransactionTypeCode = code, MatchedTransactionId = doc,
        };

    private static readonly List<ReconciliationStatementLine> Lines =
    [
        Line(1, new(2026, 4, 1), 100_000m, 0m, 100_000m, StatementLineStatus.Ignored),            // the opening balance
        Line(2, new(2026, 5, 2), 20_000m, 0m, 120_000m, StatementLineStatus.Matched, "RCM", 1),
        Line(3, new(2026, 5, 12), 0m, 5_000m, 115_000m, StatementLineStatus.Matched, "SPM", 2),
        Line(4, new(2026, 6, 2), 0m, 10_000m, 105_000m, StatementLineStatus.Matched, "TRM", 5),
        Line(5, new(2026, 6, 30), 0m, 200m, 104_800m, StatementLineStatus.Ignored),               // charges, booked by journal
        Line(6, new(2026, 7, 2), 2_500m, 0m, 107_300m, StatementLineStatus.Matched, "TRM", 6),
        Line(7, new(2026, 9, 15), 450m, 0m, 107_750m, StatementLineStatus.Unmatched),             // interest, not booked
        Line(8, new(2026, 9, 30), 0m, 1_000m, null, StatementLineStatus.Matched, "SPM", 7),       // booked on 2 October
        Line(9, new(2026, 10, 3), 7_000m, 0m, 113_750m, StatementLineStatus.Matched, "RCM", 4),   // after the day
    ];

    private static readonly List<ReconciliationStatementClosing> Closings =
    [
        new() { BankStatementId = 1, ToDate = new(2026, 9, 30), ClosingBalance = 106_750m },
    ];

    private static List<ReconciliationDocument> Documents(DateOnly asOf) =>
    [
        .. BankReconciliationReportRepository.SpendDocuments(SpendMoney.AsQueryable(), Contacts.AsQueryable(), Main, "INR", asOf),
        .. BankReconciliationReportRepository.ReceiveDocuments(ReceiveMoney.AsQueryable(), Contacts.AsQueryable(), Main, "INR", asOf),
        .. BankReconciliationReportRepository.TransferDocuments(Transfers.AsQueryable(), Banks.AsQueryable(), Main, "INR", asOf),
    ];

    private static BookBalances Book(DateOnly asOf, IEnumerable<JournalLedgerRead>? ledger = null) =>
        BankReconciliationReportRepository.BookBalance((ledger ?? Ledger).AsQueryable(), MainLedger, "INR", asOf)
            .FirstOrDefault() ?? new BookBalances();

    private static BankReconciliationReportResultDto Report(
        DateOnly? asOf = null,
        IReadOnlyList<ReconciliationStatementLine>? lines = null,
        IReadOnlyList<ReconciliationStatementClosing>? closings = null)
    {
        DateOnly day = asOf ?? AsOf;
        return BankReconciliationReportRepository.Compose(
            MainBank, Book(day), Documents(day),
            [.. (lines ?? Lines).Where(l => l.TransactionDate <= day)], closings ?? Closings, day);
    }

    private static BankReconciliationGroupDto Group(BankReconciliationReportResultDto report, BankReconciliationGroup group) =>
        report.Groups.Single(g => g.Group == group);

    // ---- What is read ------------------------------------------------------------

    [Fact]
    public void The_book_balance_is_the_ledger_account_to_the_end_of_the_day()
    {
        // 100,000 + 20,000 − 5,000 − 10,000 − 200 + 2,500 − 3,000 + 7,000; the 2 October payment is after the day.
        BookBalances book = Book(AsOf);

        Assert.Equal(111_300m, book.Balance);
        Assert.Equal(111_300m, book.BalanceBase);
    }

    [Fact]
    public void An_entry_in_another_currency_counts_at_its_base_amount_and_one_in_the_accounts_at_its_own()
    {
        List<JournalLedgerRead> ledger =
        [
            Leg(new(2026, 5, 1), 1_000m, 0m),
            Leg(new(2026, 5, 2), 100m, 0m, currency: "USD", debitBase: 8_300m),
        ];

        Assert.Equal(9_300m, Book(AsOf, ledger).Balance);

        BookBalances usd = BankReconciliationReportRepository
            .BookBalance(ledger.AsQueryable(), MainLedger, "USD", AsOf).Single();
        Assert.Equal(100m + 1_000m, usd.Balance); // the rupee entry counts at its base amount
        Assert.Equal(9_300m, usd.BalanceBase);
    }

    [Fact]
    public void Only_posted_documents_on_the_account_up_to_the_day_are_in_the_books()
    {
        List<ReconciliationDocument> documents = Documents(AsOf);

        Assert.Equal(
            ["RCM 1", "RCM 4", "SPM 2", "SPM 3", "TRM 5", "TRM 6"],
            documents.Select(d => $"{d.TransactionTypeCode} {d.TransactionId}").Order());
    }

    [Fact]
    public void Documents_are_named_by_their_contact_or_memo_and_transfers_by_the_other_account()
    {
        List<ReconciliationDocument> documents = Documents(AsOf);

        Assert.Equal("Rent Co", documents.Single(d => d.TransactionId == 2).Description);
        Assert.Equal("Cheque to plumber", documents.Single(d => d.TransactionId == 3).Description);
        Assert.Equal("Transfer to Petty Cash", documents.Single(d => d.TransactionId == 5).Description);
        Assert.Equal("Transfer from Petty Cash", documents.Single(d => d.TransactionId == 6).Description);
        Assert.True(documents.Single(d => d.TransactionId == 5).IsPayment);
        Assert.False(documents.Single(d => d.TransactionId == 6).IsPayment);
    }

    [Fact]
    public void A_document_in_another_currency_counts_at_its_rate()
    {
        List<ReceiveMoneyRead> receive =
        [
            new()
            {
                ReceiveMoneyId = 20, TransactionDate = new(2026, 8, 1), BankAccountId = Main, ContactId = 1,
                Amount = 100m, CurrencyCode = "USD", ExchangeRate = 83m, Status = MoneyDocumentStatus.Posted,
            },
        ];

        ReconciliationDocument document = BankReconciliationReportRepository
            .ReceiveDocuments(receive.AsQueryable(), Contacts.AsQueryable(), Main, "INR", AsOf).Single();

        Assert.Equal(8_300m, document.Amount);
    }

    // ---- The groups ----------------------------------------------------------------

    [Fact]
    public void A_payment_no_statement_line_shows_by_the_day_is_outstanding()
    {
        BankReconciliationGroupDto payments = Group(Report(), BankReconciliationGroup.OutstandingPayments);

        BankReconciliationItemDto cheque = Assert.Single(payments.Items);
        Assert.Equal("SPM-3", cheque.TransactionNo);
        Assert.Equal("CHQ3", cheque.Reference);
        Assert.Equal(3_000m, cheque.AmountOut);
        Assert.Equal(3_000m, payments.Adjustment);
    }

    [Fact]
    public void A_receipt_credited_after_the_day_is_outstanding_on_the_day()
    {
        BankReconciliationGroupDto receipts = Group(Report(), BankReconciliationGroup.OutstandingReceipts);

        BankReconciliationItemDto receipt = Assert.Single(receipts.Items);
        Assert.Equal("RCM-4", receipt.TransactionNo);
        Assert.Equal(7_000m, receipt.AmountIn);
        Assert.Equal(-7_000m, receipts.Adjustment);
    }

    [Fact]
    public void Unmatched_lines_and_lines_matched_to_a_document_booked_later_are_not_in_the_books()
    {
        BankReconciliationGroupDto lines = Group(Report(), BankReconciliationGroup.StatementLinesNotInBooks);

        Assert.Equal([7L, 8L], lines.Items.Select(i => i.BankStatementLineId!.Value));
        Assert.Equal(450m, lines.Items[0].AmountIn);
        Assert.Equal(1_000m, lines.Items[1].AmountOut);
        Assert.Equal(-550m, lines.Adjustment);
        Assert.All(lines.Items, i => Assert.Null(i.TransactionTypeCode));
    }

    [Fact]
    public void Ignored_lines_stand_on_neither_side()
    {
        Assert.DoesNotContain(
            Report().Groups.SelectMany(g => g.Items),
            i => i.BankStatementLineId is 1 or 5);
    }

    [Fact]
    public void The_groups_come_in_order_with_their_titles()
    {
        Assert.Equal(
            ["Payments not yet on the statement", "Receipts not yet on the statement", "Statement lines not yet in the books"],
            Report().Groups.Select(g => g.Title));
    }

    // ---- The balances --------------------------------------------------------------

    [Fact]
    public void The_books_plus_the_groups_equal_the_statement_on_the_day()
    {
        BankReconciliationReportResultDto report = Report();

        // 111,300 + 3,000 − 7,000 − 550.
        Assert.Equal(106_750m, report.ExpectedStatementBalance);
        Assert.Equal(106_750m, report.StatementBalance);
        Assert.Equal(AsOf, report.StatementBalanceDate);
        Assert.Equal(0m, report.Difference);
        Assert.True(report.IsReconciled);
        Assert.Equal("Main Bank", report.AccountName);
        Assert.Equal("INR", report.CurrencyCode);
    }

    [Fact]
    public void A_payment_cleared_after_the_day_was_outstanding_on_it()
    {
        // On 10 May the rent cheque is in the books; the bank takes it on 12 May.
        BankReconciliationReportResultDto report = Report(new(2026, 5, 10));

        Assert.Equal(115_000m, report.BookBalance);
        Assert.Equal("SPM-2", Assert.Single(Group(report, BankReconciliationGroup.OutstandingPayments).Items).TransactionNo);
        Assert.Equal(120_000m, report.ExpectedStatementBalance);
        Assert.Equal(120_000m, report.StatementBalance); // line 2's running balance on 2 May
        Assert.True(report.IsReconciled);
    }

    [Fact]
    public void A_day_with_everything_matched_or_ignored_has_nothing_between_the_two()
    {
        BankReconciliationReportResultDto report = Report(new(2026, 6, 30));

        Assert.All(report.Groups, g => Assert.Empty(g.Items));
        Assert.Equal(104_800m, report.BookBalance);
        Assert.Equal(104_800m, report.StatementBalance);
        Assert.True(report.IsReconciled);
    }

    [Fact]
    public void Without_a_closing_balance_the_last_running_balance_is_carried_forward_by_the_lines_after_it()
    {
        // Line 7's 107,750 on 15 September, less line 8's 1,000.
        Assert.Equal(
            (106_750m, new DateOnly(2026, 9, 15)),
            BankReconciliationReportRepository.StatementBalance(Lines, [], AsOf));
    }

    [Fact]
    public void A_closing_balance_later_than_the_last_running_balance_is_preferred()
    {
        List<ReconciliationStatementLine> lines =
        [
            Line(1, new(2026, 9, 1), 100m, 0m, 100m, StatementLineStatus.Matched, "RCM", 1),
            Line(2, new(2026, 9, 20), 50m, 0m, null, StatementLineStatus.Unmatched),
            Line(3, new(2026, 9, 25), 0m, 30m, null, StatementLineStatus.Unmatched, statement: 2),
        ];
        List<ReconciliationStatementClosing> closings = [new() { BankStatementId = 1, ToDate = new(2026, 9, 20), ClosingBalance = 150m }];

        // 150 at 20 September, then line 3's 30 out.
        Assert.Equal((120m, new DateOnly(2026, 9, 20)), BankReconciliationReportRepository.StatementBalance(lines, closings, AsOf));
    }

    [Fact]
    public void With_no_imported_balance_there_is_no_difference_and_nothing_reconciled()
    {
        List<ReconciliationStatementLine> noBalances = [.. Lines.Select(l => { l = Clone(l); l.RunningBalance = null; return l; })];
        BankReconciliationReportResultDto report = Report(lines: noBalances, closings: []);

        Assert.Null(report.StatementBalance);
        Assert.Null(report.StatementBalanceDate);
        Assert.Null(report.Difference);
        Assert.False(report.IsReconciled);
        Assert.Equal(106_750m, report.ExpectedStatementBalance);
    }

    [Fact]
    public void A_statement_that_disagrees_with_the_books_shows_the_difference()
    {
        BankReconciliationReportResultDto report = Report(
            closings: [new() { BankStatementId = 1, ToDate = AsOf, ClosingBalance = 106_000m }]);

        Assert.Equal(-750m, report.Difference);
        Assert.False(report.IsReconciled);
    }

    // ---- The request and the endpoint ----------------------------------------------

    [Fact]
    public void The_bank_account_and_the_as_at_date_are_required()
    {
        Assert.Equal(
            [nameof(BankReconciliationReportRequest.BankAccountId), nameof(BankReconciliationReportRequest.AsOfDate)],
            typeof(BankReconciliationReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new BankReconciliationReportController(null!)
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
            new BankReconciliationReportRequest { BankAccountId = Main, AsOfDate = AsOf, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void Both_endpoints_have_their_address_and_need_reports_and_accounting_view()
    {
        Type controller = typeof(BankReconciliationReportController);

        // The gateway routes both exact paths to Reporting; the page calls them.
        Assert.Equal("api/statements/bank-reconciliation", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("accounts", controller.GetMethod(nameof(BankReconciliationReportController.Accounts))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        foreach (string action in new[] { nameof(BankReconciliationReportController.Run), nameof(BankReconciliationReportController.Accounts) })
        {
            Assert.Contains(
                "accounting.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }
    }

    private static ReconciliationStatementLine Clone(ReconciliationStatementLine l) => new()
    {
        BankStatementLineId = l.BankStatementLineId, BankStatementId = l.BankStatementId, LineNumber = l.LineNumber,
        TransactionDate = l.TransactionDate, Description = l.Description, ReferenceNo = l.ReferenceNo,
        DepositAmount = l.DepositAmount, WithdrawalAmount = l.WithdrawalAmount, RunningBalance = l.RunningBalance,
        Status = l.Status, MatchedTransactionTypeCode = l.MatchedTransactionTypeCode, MatchedTransactionId = l.MatchedTransactionId,
    };
}

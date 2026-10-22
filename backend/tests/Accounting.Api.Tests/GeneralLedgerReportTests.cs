using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Accounting.Api.Controllers;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The General ledger's own page (TK-216), on a small set of books worked by hand.
/// The financial year starts on 1 April and the period is October 2026:
/// <list type="bullet">
/// <item>Before the financial year, in 2025-26: capital of 10,000 paid into the
/// bank, sales of 3,000 (contact 7) and rent of 1,000, so earlier years' profit is 2,000.</item>
/// <item>April to September 2026: sales of 5,000 (contact 7, INV/2627/0001) and
/// rent of 2,000 (contact 8, project 1).</item>
/// <item>October 2026: sales of 7,000 (contact 7, INV/2627/0002), returns of 400 and
/// rent of 2,500 owed to contact 8 on project 1, with no document number.</item>
/// <item>November 2026, after the period: sales of 9,000, which no figure holds.</item>
/// </list>
/// The sums run over lists through the same expressions the repository runs against
/// the database; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class GeneralLedgerReportTests
{
    private const int AssetType = 1, LiabilityType = 2, EquityType = 3, IncomeType = 4, ExpenseType = 5;
    private const long Bank = 1001, PettyCash = 1002, Dormant = 1003, Payable = 2001, Capital = 3001,
        Sales = 4001, SalesReturns = 4002, Rent = 5001;
    private const long Customer = 7, Landlord = 8, FitOut = 1;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);
    private static readonly DateOnly YearStart = new(2026, 4, 1);

    private static GeneralLedgerAccount Account(long id, int type, string name, bool contra = false, bool active = true) =>
        new() { AccountId = id, AccountTypeId = type, AccountCode = id.ToString(), AccountName = name, IsContra = contra, IsActive = active };

    private static readonly List<GeneralLedgerAccount> Accounts =
    [
        Account(Bank, AssetType, "Bank"),
        Account(PettyCash, AssetType, "Petty Cash"),
        Account(Dormant, AssetType, "Dormant Deposit", active: false),
        Account(Payable, LiabilityType, "Accounts Payable"),
        Account(Capital, EquityType, "Capital"),
        Account(Sales, IncomeType, "Sales"),
        Account(SalesReturns, IncomeType, "Sales Returns", contra: true),
        Account(Rent, ExpenseType, "Rent"),
    ];

    private static long _id;

    private static JournalLedger Leg(
        DateOnly on, long account, decimal debit, decimal credit,
        string? documentNo = null, long? contact = null, long? project = null, long? journal = null, string type = "INV", string? desc = null) =>
        new()
        {
            LedgerId = ++_id, LedgerDate = on, AccountId = account, DebitAmountBase = debit, CreditAmountBase = credit,
            DebitAmount = debit, CreditAmount = credit, TransactionTypeCode = type, TransactionId = _id, CurrencyCode = "INR",
            ExchangeRate = 1m, DocumentNo = documentNo, ContactId = contact, ProjectId = project, JournalId = journal,
            TransactionDesc = desc,
        };

    /// <summary>A balanced posting: one account debited and another credited, both naming the same contact, project and document.</summary>
    private static IEnumerable<JournalLedger> Post(
        DateOnly on, long debit, long credit, decimal amount,
        string? documentNo = null, long? contact = null, long? project = null, long? journal = null, string type = "INV", string? desc = null) =>
        [
            Leg(on, debit, amount, 0m, documentNo, contact, project, journal, type, desc),
            Leg(on, credit, 0m, amount, documentNo, contact, project, journal, type, desc),
        ];

    private static readonly List<JournalLedger> Ledger =
    [
        // Before the financial year.
        .. Post(new(2026, 1, 10), Bank, Capital, 10_000m, type: "JRN"),
        .. Post(new(2026, 2, 15), Rent, Bank, 1_000m, type: "BIL"),
        .. Post(new(2026, 2, 20), Bank, Sales, 3_000m, "INV/2526/0099", Customer),

        // April to September.
        .. Post(new(2026, 6, 10), Bank, Sales, 5_000m, "INV/2627/0001", Customer, desc: "Counter sale"),
        .. Post(new(2026, 7, 5), Rent, Bank, 2_000m, "BIL/2627/0001", Landlord, FitOut, type: "BIL"),

        // October.
        .. Post(new(2026, 10, 3), Bank, Sales, 7_000m, "INV/2627/0002", Customer, desc: "Invoice to Customer"),
        .. Post(new(2026, 10, 12), SalesReturns, Bank, 400m, "CN/2627/0001", Customer, type: "CRN"),
        .. Post(new(2026, 10, 20), Rent, Payable, 2_500m, null, Landlord, FitOut, journal: 11, type: "JRN", desc: "October rent"),

        // November, after the period.
        .. Post(new(2026, 11, 5), Bank, Sales, 9_000m, "INV/2627/0003", Customer),
    ];

    private static readonly List<Journal> Journals = [new() { JournalId = 11, JournalNo = "JV-0011" }];

    private static readonly List<Project> Projects = [new() { ProjectId = FitOut, ProjectCode = "PRJ-1", ProjectName = "Shop fit-out" }];

    /// <summary>One account's ledger, through the same builders the repository runs.</summary>
    private static GeneralLedgerReportResultDto Run(
        long accountId, DateOnly from, DateOnly to, long? contactId = null, long? projectId = null)
    {
        GeneralLedgerAccount account = Accounts.Single(a => a.AccountId == accountId);
        DateOnly yearStart = TrialBalanceReportRepository.FinancialYearStart(from, 4);

        IQueryable<JournalLedger> postings = GeneralLedgerReportRepository.Postings(Ledger.AsQueryable(), accountId, contactId, projectId);
        decimal openingNet = GeneralLedgerReportRepository.OpeningNets(
            postings, from, GeneralLedgerReportRepository.IsProfitAndLoss(account.AccountTypeId) ? yearStart : null).Sum();
        List<GeneralLedgerReportRowDto> rows = [.. GeneralLedgerReportRepository.Rows(
            postings, new List<SubAccount>().AsQueryable(), Journals.AsQueryable(), Projects.AsQueryable(), from, to)];

        return GeneralLedgerReportRepository.Compose(account, rows, openingNet, from, to, yearStart, contactId, projectId);
    }

    private static List<AccountPeriodSums> SumsFor(DateOnly from, DateOnly to) =>
        [.. TrialBalanceReportRepository.Sums(Ledger.AsQueryable(), from, to, TrialBalanceReportRepository.FinancialYearStart(from, 4), null, null, null)];

    private static GeneralLedgerSummaryResultDto Summary(bool zeroShown = false) =>
        GeneralLedgerReportRepository.ComposeSummary(Accounts, SumsFor(October1, October31), October1, October31, YearStart, zeroShown);

    // ---- One account, worked by hand ------------------------------------------------

    [Fact]
    public void A_credit_account_reads_on_the_credit_side_with_a_running_balance()
    {
        GeneralLedgerReportResultDto report = Run(Sales, October1, October31);

        // Sales: 5,000 since 1 April (the 3,000 of 2025-26 is earlier years' profit, not opened), 7,000 in October.
        Assert.Equal(LedgerSide.Credit, report.NormalSide);
        Assert.Equal(5_000m, report.OpeningBalance);
        GeneralLedgerReportRowDto row = Assert.Single(report.Rows);
        Assert.Equal(12_000m, row.RunningBalance);
        Assert.Equal(12_000m, report.ClosingBalance);
        Assert.Equal((0m, 7_000m), (report.TotalDebit, report.TotalCredit));
    }

    [Fact]
    public void A_debit_account_runs_down_the_page_from_what_it_opened_at()
    {
        GeneralLedgerReportResultDto report = Run(Bank, October1, October31);

        // 10,000 - 1,000 + 3,000 + 5,000 - 2,000 = 15,000 before October; then +7,000 and -400.
        Assert.Equal(LedgerSide.Debit, report.NormalSide);
        Assert.Equal(15_000m, report.OpeningBalance);
        Assert.Equal([22_000m, 21_600m], report.Rows.Select(r => r.RunningBalance));
        Assert.Equal(21_600m, report.ClosingBalance);
        Assert.Equal((7_000m, 400m), (report.TotalDebit, report.TotalCredit));
    }

    [Fact]
    public void A_contra_account_reads_on_the_side_opposite_its_type()
    {
        GeneralLedgerReportResultDto report = Run(SalesReturns, October1, October31);

        // Returns are income by type, which grows on the credit side, and a contra account runs the other way.
        Assert.True(report.IsContra);
        Assert.Equal(LedgerSide.Debit, report.NormalSide);
        Assert.Equal(0m, report.OpeningBalance);
        Assert.Equal(400m, report.ClosingBalance);
        Assert.Equal(400m, Assert.Single(report.Rows).RunningBalance);
    }

    [Theory]
    [InlineData(AssetType, false, LedgerSide.Debit)]
    [InlineData(LiabilityType, false, LedgerSide.Credit)]
    [InlineData(EquityType, false, LedgerSide.Credit)]
    [InlineData(IncomeType, false, LedgerSide.Credit)]
    [InlineData(ExpenseType, false, LedgerSide.Debit)]
    [InlineData(AssetType, true, LedgerSide.Credit)]
    [InlineData(IncomeType, true, LedgerSide.Debit)]
    [InlineData(ExpenseType, true, LedgerSide.Credit)]
    public void The_normal_side_is_the_types_turned_over_for_a_contra_account(int type, bool contra, LedgerSide expected) =>
        Assert.Equal(expected, GeneralLedgerReportRepository.NormalSideOf(type, contra));

    [Fact]
    public void Every_account_satisfies_opening_plus_what_grew_it_less_what_reduced_it_equals_closing()
    {
        foreach (GeneralLedgerAccount account in Accounts)
        {
            GeneralLedgerReportResultDto report = Run(account.AccountId, October1, October31);
            bool debitNormal = report.NormalSide == LedgerSide.Debit;
            decimal grew = debitNormal ? report.TotalDebit : report.TotalCredit;
            decimal reduced = debitNormal ? report.TotalCredit : report.TotalDebit;

            Assert.Equal(report.OpeningBalance + grew - reduced, report.ClosingBalance);
        }
    }

    [Fact]
    public void A_balance_sheet_account_opens_at_everything_before_and_an_income_or_expense_account_since_the_year_began()
    {
        // Rent is an expense: 2,000 since 1 April, not the 3,000 posted before October, and the last row takes it to 4,500.
        GeneralLedgerReportResultDto rent = Run(Rent, October1, October31);
        Assert.Equal(2_000m, rent.OpeningBalance);
        Assert.Equal(4_500m, rent.ClosingBalance);
        Assert.Equal(YearStart, rent.FinancialYearStart);

        // Capital is equity: everything before the period, whenever it was.
        Assert.Equal(10_000m, Run(Capital, October1, October31).OpeningBalance);
    }

    [Fact]
    public void An_account_with_nothing_in_the_period_closes_where_it_opened()
    {
        GeneralLedgerReportResultDto capital = Run(Capital, October1, October31);

        Assert.Empty(capital.Rows);
        Assert.Equal((10_000m, 10_000m), (capital.OpeningBalance, capital.ClosingBalance));
        Assert.Equal((0m, 0m), (capital.TotalDebit, capital.TotalCredit));
    }

    [Fact]
    public void Postings_after_the_period_are_in_neither_the_rows_nor_the_balances()
    {
        GeneralLedgerReportResultDto sales = Run(Sales, October1, October31);

        Assert.DoesNotContain(sales.Rows, r => r.LedgerDate > October31);
        Assert.DoesNotContain(sales.Rows, r => r.DocumentNo == "INV/2627/0003");
        Assert.Equal(12_000m, sales.ClosingBalance);
    }

    // ---- What a row says ------------------------------------------------------------

    [Fact]
    public void A_row_names_its_document_and_what_it_was_for()
    {
        GeneralLedgerReportRowDto row = Assert.Single(Run(Sales, October1, October31).Rows);

        Assert.Equal("INV/2627/0002", row.DocumentNo);
        Assert.Equal("INV", row.TransactionTypeCode);
        Assert.Equal(Customer, row.ContactId);
        Assert.Equal("Invoice to Customer", row.Description);
    }

    [Fact]
    public void A_row_written_before_the_ledger_carried_a_number_gets_its_type_and_id()
    {
        GeneralLedgerReportRowDto row = Run(Payable, October1, October31).Rows.Single();

        Assert.Matches(@"^JRN-\d+$", row.DocumentNo);
        Assert.Equal("JRN-" + row.TransactionId, row.DocumentNo);

        Assert.Equal("BIL-9", GeneralLedgerReportRepository.DocumentNoOf(null, "BIL", 9));
        Assert.Equal("BIL-9", GeneralLedgerReportRepository.DocumentNoOf("  ", "BIL", 9));
        Assert.Equal("INV/1", GeneralLedgerReportRepository.DocumentNoOf("INV/1", "BIL", 9));
    }

    [Fact]
    public void A_row_carries_the_journal_and_the_project_it_names()
    {
        GeneralLedgerReportRowDto row = Run(Payable, October1, October31).Rows.Single();

        Assert.Equal((11L, "JV-0011"), (row.JournalId, row.JournalNo));

        GeneralLedgerReportRowDto rent = Run(Rent, October1, October31).Rows.Single();
        Assert.Equal((FitOut, "Shop fit-out"), (rent.ProjectId, rent.ProjectName));
    }

    [Fact]
    public void A_row_with_no_sub_account_journal_or_project_is_still_listed()
    {
        // The left joins keep every posting; an inner join would drop the bank and sales legs.
        GeneralLedgerReportRowDto row = Run(Sales, October1, October31).Rows.Single();

        Assert.Null(row.JournalId);
        Assert.Null(row.JournalNo);
        Assert.Null(row.ProjectName);
        Assert.Null(row.SubAccountName);
    }

    [Fact]
    public void Postings_come_in_date_order()
    {
        GeneralLedgerReportResultDto bank = Run(Bank, new DateOnly(2026, 1, 1), October31);

        Assert.Equal(bank.Rows.OrderBy(r => r.LedgerDate).ThenBy(r => r.LedgerId).Select(r => r.LedgerId), bank.Rows.Select(r => r.LedgerId));
    }

    // ---- A contact and a project ----------------------------------------------------

    [Fact]
    public void A_contact_narrows_the_ledger_and_its_opening_balance_to_the_same_postings()
    {
        // Sales to the customer: 5,000 since 1 April, and 7,000 in October.
        GeneralLedgerReportResultDto customer = Run(Sales, October1, October31, contactId: Customer);
        Assert.Equal((5_000m, 12_000m), (customer.OpeningBalance, customer.ClosingBalance));
        Assert.Equal(Customer, customer.ContactId);

        // Nothing was sold to the landlord, so the ledger is empty and opens at nothing.
        GeneralLedgerReportResultDto landlord = Run(Sales, October1, October31, contactId: Landlord);
        Assert.Empty(landlord.Rows);
        Assert.Equal((0m, 0m), (landlord.OpeningBalance, landlord.ClosingBalance));
    }

    [Fact]
    public void A_project_narrows_the_ledger_and_its_opening_balance_to_the_same_postings()
    {
        // Rent on the fit-out: 2,000 since 1 April (the 1,000 of 2025-26 names no project), 2,500 in October.
        GeneralLedgerReportResultDto fitOut = Run(Rent, October1, October31, projectId: FitOut);
        Assert.Equal((2_000m, 4_500m), (fitOut.OpeningBalance, fitOut.ClosingBalance));
        Assert.Equal(FitOut, fitOut.ProjectId);

        GeneralLedgerReportResultDto other = Run(Rent, October1, October31, projectId: 99);
        Assert.Empty(other.Rows);
        Assert.Equal(0m, other.ClosingBalance);
    }

    [Fact]
    public void A_contact_and_a_project_together_keep_only_the_postings_that_name_both()
    {
        Assert.Single(Run(Payable, October1, October31, contactId: Landlord, projectId: FitOut).Rows);
        Assert.Empty(Run(Payable, October1, October31, contactId: Customer, projectId: FitOut).Rows);
    }

    // ---- The running balance, on its own --------------------------------------------

    [Fact]
    public void The_running_balance_starts_at_the_opening_balance_and_turns_with_the_side()
    {
        List<GeneralLedgerReportRowDto> rows =
        [
            new() { DebitAmountBase = 100m },
            new() { CreditAmountBase = 40m },
        ];

        decimal debitClosing = GeneralLedgerReportRepository.ApplyRunningBalance(rows, 10m, LedgerSide.Debit);
        Assert.Equal([110m, 70m], rows.Select(r => r.RunningBalance));
        Assert.Equal(70m, debitClosing);

        decimal creditClosing = GeneralLedgerReportRepository.ApplyRunningBalance(rows, 10m, LedgerSide.Credit);
        Assert.Equal([-90m, -50m], rows.Select(r => r.RunningBalance));
        Assert.Equal(-50m, creditClosing);

        Assert.Equal(10m, GeneralLedgerReportRepository.ApplyRunningBalance([], 10m, LedgerSide.Debit));
    }

    // ---- The summary of every account -----------------------------------------------

    [Fact]
    public void The_summary_lists_each_account_that_moved_or_holds_a_balance_in_code_order()
    {
        GeneralLedgerSummaryResultDto summary = Summary();

        Assert.Equal([Bank, Payable, Capital, Sales, SalesReturns, Rent], summary.Lines.Select(l => l.AccountId));

        GeneralLedgerSummaryLineDto sales = summary.Lines.Single(l => l.AccountId == Sales);
        Assert.Equal((LedgerSide.Credit, 5_000m, 0m, 7_000m, 12_000m),
            (sales.NormalSide, sales.OpeningBalance, sales.TotalDebit, sales.TotalCredit, sales.ClosingBalance));

        GeneralLedgerSummaryLineDto bank = summary.Lines.Single(l => l.AccountId == Bank);
        Assert.Equal((15_000m, 7_000m, 400m, 21_600m),
            (bank.OpeningBalance, bank.TotalDebit, bank.TotalCredit, bank.ClosingBalance));

        GeneralLedgerSummaryLineDto payable = summary.Lines.Single(l => l.AccountId == Payable);
        Assert.Equal((0m, 0m, 2_500m, 2_500m),
            (payable.OpeningBalance, payable.TotalDebit, payable.TotalCredit, payable.ClosingBalance));
    }

    [Fact]
    public void The_summarys_period_debits_and_credits_agree_on_books_that_balance()
    {
        GeneralLedgerSummaryResultDto summary = Summary();

        Assert.Equal(9_900m, summary.TotalDebit);
        Assert.Equal(summary.TotalDebit, summary.TotalCredit);
    }

    [Fact]
    public void An_account_with_nothing_posted_is_left_out_unless_zero_balances_are_asked_for()
    {
        Assert.DoesNotContain(Summary().Lines, l => l.AccountId == PettyCash);

        GeneralLedgerSummaryResultDto all = Summary(zeroShown: true);

        Assert.Contains(all.Lines, l => l.AccountId == PettyCash);

        // An inactive account with nothing in it stays off even then: it cannot be posted to.
        Assert.DoesNotContain(all.Lines, l => l.AccountId == Dormant);
    }

    [Fact]
    public void An_account_with_no_postings_in_the_summary_period_is_given_no_ledger_rows()
    {
        // The ledger and the summary agree about an account the period did not touch.
        GeneralLedgerSummaryLineDto capital = Summary().Lines.Single(l => l.AccountId == Capital);

        Assert.Equal((10_000m, 10_000m), (capital.OpeningBalance, capital.ClosingBalance));
        Assert.Equal((0m, 0m), (capital.TotalDebit, capital.TotalCredit));
    }

    [Fact]
    public void The_summary_and_the_ledger_of_an_account_give_the_same_figures()
    {
        foreach (GeneralLedgerSummaryLineDto line in Summary().Lines)
        {
            GeneralLedgerReportResultDto ledger = Run(line.AccountId, October1, October31);

            Assert.Equal(
                (ledger.OpeningBalance, ledger.TotalDebit, ledger.TotalCredit, ledger.ClosingBalance),
                (line.OpeningBalance, line.TotalDebit, line.TotalCredit, line.ClosingBalance));
        }
    }

    [Fact]
    public void An_accounts_closing_balance_is_its_closing_balance_on_the_trial_balance_for_the_same_dates()
    {
        TrialBalanceReportResultDto trial = TrialBalanceReportRepository.Compose(
            [.. Accounts.Select(a => new TrialBalanceAccount
            {
                AccountId = a.AccountId, AccountTypeId = a.AccountTypeId, AccountCode = a.AccountCode,
                AccountName = a.AccountName, IsContra = a.IsContra, IsActive = a.IsActive,
            })],
            SumsFor(October1, October31), October1, October31, YearStart, null, null, null, isZeroBalanceShown: false);

        foreach (TrialBalanceReportLineDto line in trial.Groups.SelectMany(g => g.Lines).Where(l => !l.IsComputed))
        {
            GeneralLedgerReportResultDto ledger = Run(line.AccountId, October1, October31);

            // The trial balance's net is in debit terms; the ledger's is on the account's own side.
            Assert.Equal(
                GeneralLedgerReportRepository.OnSide(line.ClosingDebit - line.ClosingCredit, ledger.NormalSide),
                ledger.ClosingBalance);
            Assert.Equal(
                GeneralLedgerReportRepository.OnSide(line.OpeningDebit - line.OpeningCredit, ledger.NormalSide),
                ledger.OpeningBalance);
        }
    }

    // ---- The requests and the guards ------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_on_both_requests_and_nothing_else_is()
    {
        foreach (Type request in new[] { typeof(GeneralLedgerReportRequest), typeof(GeneralLedgerSummaryRequest) })
        {
            Assert.Equal(
                ["From", "To"],
                request.GetProperties().Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null).Select(p => p.Name));
        }
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new GeneralLedgerReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date()
    {
        Assert.Equal(
            [(nameof(GeneralLedgerReportRequest.To), "To date cannot be before the from date.")],
            GeneralLedgerReportRepository.DateRefusals(October31, October1));

        Assert.Empty(GeneralLedgerReportRepository.DateRefusals(October1, October31));
        Assert.Empty(GeneralLedgerReportRepository.DateRefusals(October1, October1));
    }

    private static LedgerController ControllerForAnotherBranch() =>
        new(null!, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("org_id", Guid.NewGuid().ToString())], "test")),
                },
            },
        };

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        IActionResult ledger = await ControllerForAnotherBranch().AccountLedger(
            Sales, new GeneralLedgerReportRequest { From = October1, To = October31, OrgId = Guid.NewGuid() }, default);
        IActionResult summary = await ControllerForAnotherBranch().Summary(
            new GeneralLedgerSummaryRequest { From = October1, To = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(ledger);
        Assert.IsType<ForbidResult>(summary);
    }

    [Fact]
    public void The_ledger_and_the_summary_are_at_their_addresses_under_the_ledger_guards()
    {
        Type controller = typeof(LedgerController);

        Assert.Equal("accounting", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);

        MethodInfo ledger = controller.GetMethod(nameof(LedgerController.AccountLedger))!;
        MethodInfo summary = controller.GetMethod(nameof(LedgerController.Summary))!;

        Assert.Equal("accounts/{accountId:long}", ledger.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("summary", summary.GetCustomAttribute<HttpGetAttribute>()!.Template);

        // Both take a request model with required dates, not two optional ones.
        Assert.Equal(typeof(GeneralLedgerReportRequest), ledger.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(GeneralLedgerSummaryRequest), summary.GetParameters()[0].ParameterType);
    }
}

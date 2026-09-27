using Reporting.Api.Services.Sources;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The project reports (TK-107), over lists.
///
/// <b>What this proves and what it does not.</b> It proves the arithmetic:
/// which ledger rows count as a project's revenue and cost, what is still
/// unbilled and at what value, how hours split. It does not prove the queries
/// translate, since LINQ to Objects will run anything, so the four reports
/// still need running against a real <c>acc</c> schema.
/// </summary>
public sealed class ProjectReportTests
{
    private const long SalesAccount = 1;
    private const long ReturnsAccount = 2;
    private const long MaterialsAccount = 3;
    private const long BankAccount = 4;
    private const long ReceivableAccount = 5;

    private static readonly Guid Asha = Guid.NewGuid();
    private static readonly Guid Ravi = Guid.NewGuid();

    private static readonly List<AccountRead> Accounts =
    [
        new() { AccountId = SalesAccount, AccountTypeId = 4, AccountCode = "4000", AccountName = "Sales" },
        new() { AccountId = ReturnsAccount, AccountTypeId = 4, IsContra = true, AccountCode = "4100", AccountName = "Sales returns" },
        new() { AccountId = MaterialsAccount, AccountTypeId = 5, AccountCode = "5000", AccountName = "Materials" },
        new() { AccountId = BankAccount, AccountTypeId = 1, AccountCode = "1000", AccountName = "Bank" },
        new() { AccountId = ReceivableAccount, AccountTypeId = 1, AccountCode = "1100", AccountName = "Receivables" },
    ];

    private static readonly List<ContactRead> Contacts =
    [
        new() { ContactId = 7, ContactCode = "C7", DisplayName = "Acme Interiors", CurrencyCode = "INR" },
    ];

    // ---- profitability ---------------------------------------------------------

    [Fact]
    public void Profitability_equals_the_projects_ledger_rows_net_by_type()
    {
        List<JournalLedgerRead> ledger =
        [
            Leg(1, SalesAccount, credit: 10_000m),
            Leg(1, ReturnsAccount, debit: 1_000m),
            Leg(1, MaterialsAccount, debit: 4_000m),
            Leg(1, MaterialsAccount, credit: 500m),
            Leg(1, ReceivableAccount, debit: 9_000m),
            Leg(1, BankAccount, credit: 3_500m),
            Leg(2, SalesAccount, credit: 99_000m),
            Leg(null, MaterialsAccount, debit: 77_000m),
        ];

        ProjectProfitabilityRow row = ProfitabilityOf(1, ledger, null, null);

        // Income: 10,000 credit less the 1,000 return. Expense: 4,000 less 500.
        Assert.Equal(9_000m, row.Revenue);
        Assert.Equal(3_500m, row.Cost);
        Assert.Equal(5_500m, row.Profit);
        Assert.Equal(61.11m, row.MarginPercent);
        Assert.Equal("Acme Interiors", row.Customer);

        decimal byType = ledger
            .Where(l => l.ProjectId == 1)
            .Join(Accounts, l => l.AccountId, a => a.AccountId, (l, a) => a.AccountTypeId switch
            {
                4 => l.CreditAmountBase - l.DebitAmountBase,
                5 => -(l.DebitAmountBase - l.CreditAmountBase),
                _ => 0m,
            })
            .Sum();

        Assert.Equal(byType, row.Profit);
    }

    [Fact]
    public void Profitability_counts_only_rows_inside_the_period()
    {
        List<JournalLedgerRead> ledger =
        [
            Leg(1, SalesAccount, credit: 1_000m, on: new DateOnly(2026, 3, 31)),
            Leg(1, SalesAccount, credit: 2_000m, on: new DateOnly(2026, 4, 1)),
            Leg(1, MaterialsAccount, debit: 700m, on: new DateOnly(2026, 4, 30)),
            Leg(1, MaterialsAccount, debit: 900m, on: new DateOnly(2026, 5, 1)),
        ];

        ProjectProfitabilityRow row = ProfitabilityOf(1, ledger, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        Assert.Equal(2_000m, row.Revenue);
        Assert.Equal(700m, row.Cost);
    }

    [Fact]
    public void A_project_with_no_revenue_has_no_margin_and_an_internal_one_no_customer()
    {
        List<JournalLedgerRead> ledger = [Leg(3, MaterialsAccount, debit: 250m)];

        ProjectProfitabilityRow row = ProfitabilityOf(3, ledger, null, null);

        Assert.Equal(-250m, row.Profit);
        Assert.Null(row.MarginPercent);
        Assert.Equal("Internal", row.Customer);
    }

    // ---- budget against actual -------------------------------------------------

    [Fact]
    public void Budget_against_actual_compares_cost_and_hours()
    {
        List<ProjectTaskRead> tasks =
        [
            new() { ProjectTaskId = 11, ProjectId = 1, TaskName = "Design", BudgetHours = 10m },
            new() { ProjectTaskId = 12, ProjectId = 1, TaskName = "Build", BudgetHours = 30m },
            new() { ProjectTaskId = 13, ProjectId = 1, TaskName = "Snags" },
        ];
        List<TimeEntryRead> time =
        [
            Time(1, 11, Asha, 12m),
            Time(1, 12, Ravi, 20.5m),
            Time(2, 21, Ravi, 8m),
        ];
        List<JournalLedgerRead> ledger =
        [
            Leg(1, MaterialsAccount, debit: 6_000m),
            Leg(1, SalesAccount, credit: 50_000m),
        ];

        ProjectBudgetRow row = Assert.Single(
            ProjectReports.BudgetAgainstActual(Projects().AsQueryable(), tasks.AsQueryable(), time.AsQueryable(), ledger.AsQueryable(), Accounts.AsQueryable()),
            r => r.ProjectId == 1);

        Assert.Equal(8_000m, row.BudgetAmount);
        Assert.Equal(6_000m, row.ActualCost);
        Assert.Equal(2_000m, row.CostVariance);
        Assert.Equal(75m, row.CostUsedPercent);
        Assert.Equal(40m, row.BudgetHours);
        Assert.Equal(32.5m, row.ActualHours);
        Assert.Equal(7.5m, row.HoursVariance);
    }

    [Fact]
    public void A_project_with_no_budget_has_no_variance()
    {
        ProjectBudgetRow row = Assert.Single(
            ProjectReports.BudgetAgainstActual(
                Projects().AsQueryable(),
                new List<ProjectTaskRead>().AsQueryable(),
                new List<TimeEntryRead>().AsQueryable(),
                new List<JournalLedgerRead> { Leg(3, MaterialsAccount, debit: 100m) }.AsQueryable(),
                Accounts.AsQueryable()),
            r => r.ProjectId == 3);

        Assert.Null(row.BudgetAmount);
        Assert.Null(row.CostVariance);
        Assert.Null(row.CostUsedPercent);
        Assert.Equal(100m, row.ActualCost);
    }

    // ---- unbilled work ---------------------------------------------------------

    [Fact]
    public void Unbilled_work_values_hours_expenses_and_milestones_as_an_invoice_would()
    {
        List<ProjectTaskRead> tasks = [new() { ProjectTaskId = 11, ProjectId = 1, TaskName = "Design", HourlyRate = 900m }];
        List<TimeEntryRead> time =
        [
            Time(1, 11, Asha, 2m),
            Time(1, 11, Ravi, 1.5m),
            Time(1, 11, Asha, 4m, billable: false),
            Time(1, 11, Asha, 3m, status: "Billed"),
        ];
        List<SpendMoneyRead> spend =
        [
            new() { SpendMoneyId = 50, Status = 1, CurrencyCode = "INR", ExchangeRate = 1m },
            new() { SpendMoneyId = 51, Status = 0, CurrencyCode = "INR", ExchangeRate = 1m },
        ];
        List<SpendMoneyDetailRead> spendLines =
        [
            new() { SpendMoneyDetailId = 1, SpendMoneyId = 50, ProjectId = 1, IsBillable = true, Amount = 1_000m, AmountBase = 1_000m, MarkupPercent = 10m },
            new() { SpendMoneyDetailId = 2, SpendMoneyId = 50, ProjectId = 1, IsBillable = true, Amount = 400m, AmountBase = 400m, BilledInvoiceId = 9 },
            new() { SpendMoneyDetailId = 3, SpendMoneyId = 51, ProjectId = 1, IsBillable = true, Amount = 300m, AmountBase = 300m },
            new() { SpendMoneyDetailId = 4, SpendMoneyId = 50, ProjectId = 1, IsBillable = false, Amount = 200m, AmountBase = 200m },
        ];
        List<BillRead> bills =
        [
            new() { BillId = 60, DocumentNo = "BIL-1", TransactionTypeCode = "BIL", CurrencyCode = "USD", ExchangeRate = 80m, Status = DocumentStatus.Posted },
        ];
        List<BillDetailRead> billLines =
        [
            new() { BillDetailId = 1, BillId = 60, ProjectId = 1, IsBillable = true, TaxableAmount = 10m, MarkupPercent = 25m },
        ];
        List<ProjectMilestoneRead> milestones =
        [
            new() { ProjectMilestoneId = 1, ProjectId = 1, Amount = 5_000m },
            new() { ProjectMilestoneId = 2, ProjectId = 1, Amount = 7_000m, InvoiceId = 9 },
        ];

        ProjectUnbilledRow row = Assert.Single(ProjectReports.Unbilled(
            Projects().AsQueryable(),
            Contacts.AsQueryable(),
            tasks.AsQueryable(),
            new List<ProjectMemberRead>().AsQueryable(),
            time.AsQueryable(),
            spendLines.AsQueryable(),
            spend.AsQueryable(),
            billLines.AsQueryable(),
            bills.AsQueryable(),
            milestones.AsQueryable()));

        // Project 1 bills at the task rate: 3.5 unbilled billable hours at 900.
        Assert.Equal(3.5m, row.UnbilledHours);
        Assert.Equal(3_150m, row.TimeValue);

        // 1,000 at 10% markup, plus a $10 bill line at 80 with 25% markup.
        Assert.Equal(1_100m + 1_000m, row.Expenses);
        Assert.Equal(5_000m, row.Milestones);
        Assert.Equal(3_150m + 2_100m + 5_000m, row.Total);
    }

    [Fact]
    public void Unbilled_hours_take_the_persons_rate_on_a_per_person_project()
    {
        List<ProjectRead> projects = [Project(4, "PRJ-0004", rateBasis: "UserRate", hourlyRate: 100m)];
        List<ProjectTaskRead> tasks = [new() { ProjectTaskId = 41, ProjectId = 4, TaskName = "Consulting", HourlyRate = 5m }];
        List<ProjectMemberRead> members = [new() { ProjectMemberId = 1, ProjectId = 4, UserId = Asha, HourlyRate = 1_200m }];
        List<TimeEntryRead> time = [Time(4, 41, Asha, 2m), Time(4, 41, Ravi, 1m)];

        ProjectUnbilledRow row = Assert.Single(ProjectReports.Unbilled(
            projects.AsQueryable(),
            Contacts.AsQueryable(),
            tasks.AsQueryable(),
            members.AsQueryable(),
            time.AsQueryable(),
            new List<SpendMoneyDetailRead>().AsQueryable(),
            new List<SpendMoneyRead>().AsQueryable(),
            new List<BillDetailRead>().AsQueryable(),
            new List<BillRead>().AsQueryable(),
            new List<ProjectMilestoneRead>().AsQueryable()));

        // Asha at her 1,200; Ravi is no member, so his hour has no rate.
        Assert.Equal(3m, row.UnbilledHours);
        Assert.Equal(2_400m, row.TimeValue);
    }

    [Fact]
    public void A_project_with_nothing_unbilled_is_left_out()
    {
        List<TimeEntryRead> time = [Time(1, 11, Asha, 2m, status: "Billed")];

        Assert.Empty(ProjectReports.Unbilled(
            Projects().AsQueryable(),
            Contacts.AsQueryable(),
            new List<ProjectTaskRead> { new() { ProjectTaskId = 11, ProjectId = 1, TaskName = "Design" } }.AsQueryable(),
            new List<ProjectMemberRead>().AsQueryable(),
            time.AsQueryable(),
            new List<SpendMoneyDetailRead>().AsQueryable(),
            new List<SpendMoneyRead>().AsQueryable(),
            new List<BillDetailRead>().AsQueryable(),
            new List<BillRead>().AsQueryable(),
            new List<ProjectMilestoneRead>().AsQueryable()));
    }

    // ---- time by user ----------------------------------------------------------

    [Fact]
    public void Time_by_user_splits_hours_per_task_and_person_inside_the_period()
    {
        List<ProjectTaskRead> tasks =
        [
            new() { ProjectTaskId = 11, ProjectId = 1, TaskName = "Design" },
            new() { ProjectTaskId = 12, ProjectId = 1, TaskName = "Build" },
        ];
        List<TimeEntryRead> time =
        [
            Time(1, 11, Asha, 2m, on: new DateOnly(2026, 9, 21)),
            Time(1, 11, Asha, 3m, on: new DateOnly(2026, 9, 22), status: "Billed"),
            Time(1, 11, Asha, 1m, on: new DateOnly(2026, 9, 23), billable: false),
            Time(1, 11, Ravi, 4m, on: new DateOnly(2026, 9, 22)),
            Time(1, 12, Asha, 6m, on: new DateOnly(2026, 9, 22)),
            Time(1, 11, Asha, 9m, on: new DateOnly(2026, 10, 1)),
        ];

        List<ProjectTimeByUserRow> rows = [.. ProjectReports.TimeByUser(
            Projects().AsQueryable(), tasks.AsQueryable(), time.AsQueryable(), new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27))];

        Assert.Equal(3, rows.Count);

        ProjectTimeByUserRow design = Assert.Single(rows, r => r.TaskName == "Design" && r.UserId == Asha);
        Assert.Equal(6m, design.Hours);
        Assert.Equal(5m, design.BillableHours);
        Assert.Equal(3m, design.BilledHours);
        Assert.Equal(1m, design.NonBillableHours);
        Assert.Equal(rows.Count, rows.Select(r => r.RowKey).Distinct().Count());
    }

    // ---- fixtures --------------------------------------------------------------

    private static ProjectProfitabilityRow ProfitabilityOf(long projectId, List<JournalLedgerRead> ledger, DateOnly? from, DateOnly? to) =>
        Assert.Single(
            ProjectReports.Profitability(Projects().AsQueryable(), Contacts.AsQueryable(), ledger.AsQueryable(), Accounts.AsQueryable(), from, to),
            r => r.ProjectId == projectId);

    private static List<ProjectRead> Projects() =>
    [
        Project(1, "PRJ-0001", contactId: 7, rateBasis: "TaskRate", budget: 8_000m),
        Project(2, "PRJ-0002", contactId: 7, rateBasis: "ProjectRate", hourlyRate: 500m),
        Project(3, "PRJ-0003", billingMethod: "NonBillable"),
    ];

    private static ProjectRead Project(
        long id,
        string code,
        long? contactId = null,
        string billingMethod = "TimeAndMaterials",
        string? rateBasis = null,
        decimal? hourlyRate = null,
        decimal? budget = null) =>
        new()
        {
            ProjectId = id,
            ProjectCode = code,
            ProjectName = $"Job {id}",
            ContactId = contactId,
            BillingMethod = billingMethod,
            RateBasis = rateBasis,
            HourlyRate = hourlyRate,
            BudgetAmount = budget,
            Status = "Active",
        };

    private static long _ledgerId;

    private static JournalLedgerRead Leg(long? projectId, long accountId, decimal debit = 0m, decimal credit = 0m, DateOnly? on = null) =>
        new()
        {
            LedgerId = Interlocked.Increment(ref _ledgerId),
            LedgerDate = on ?? new DateOnly(2026, 9, 1),
            AccountId = accountId,
            TransactionTypeCode = "JNL",
            DebitAmount = debit,
            CreditAmount = credit,
            DebitAmountBase = debit,
            CreditAmountBase = credit,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            ProjectId = projectId,
        };

    private static long _timeId;

    private static TimeEntryRead Time(long projectId, long taskId, Guid userId, decimal hours, bool billable = true, string status = "Unbilled", DateOnly? on = null) =>
        new()
        {
            TimeEntryId = Interlocked.Increment(ref _timeId),
            ProjectId = projectId,
            ProjectTaskId = taskId,
            UserId = userId,
            WorkDate = on ?? new DateOnly(2026, 9, 21),
            Hours = hours,
            IsBillable = billable,
            BillingStatus = status,
        };
}

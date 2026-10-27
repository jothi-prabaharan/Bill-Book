using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;

namespace Reporting.Repository.Repositories;

// -----------------------------------------------------------------------------
// Project reports (TK-107): profitability, budget against actual, unbilled
// work and time by user. The queries are here as functions of plain
// IQueryables so the arithmetic can be tested over lists; each source only
// hands them its context's sets.
// -----------------------------------------------------------------------------

/// <summary>The account types the project reports read by id (<c>mst.AccountTypes</c>).</summary>
internal static class ProjectAccountTypes
{
    public const int Income = 4;
    public const int Expense = 5;
}

/// <summary>The project reports' queries, composed and never executed here.</summary>
public static class ProjectReports
{
    /// <summary>
    /// One row per project: its income and expense ledger rows netted by type
    /// over the period. Income is credit less debit and expense debit less
    /// credit, so a contra account (sales returns, discount given) nets itself
    /// off without a sign rule of its own.
    /// </summary>
    public static IQueryable<ProjectProfitabilityRow> Profitability(
        IQueryable<ProjectRead> projects,
        IQueryable<ContactRead> contacts,
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<AccountRead> accounts,
        DateOnly? start,
        DateOnly? end)
    {
        IQueryable<JournalLedgerRead> legs = ledger.Where(l =>
            l.ProjectId != null
            && (start == null || l.LedgerDate >= start)
            && (end == null || l.LedgerDate <= end));

        IQueryable<ProjectProfitabilityRow> rows =
            from p in projects
            join c in contacts on p.ContactId equals c.ContactId into gc
            from c in gc.DefaultIfEmpty()
            select new ProjectProfitabilityRow
            {
                ProjectId = p.ProjectId,
                ProjectCode = p.ProjectCode,
                ProjectName = p.ProjectName,
                Customer = c != null ? c.DisplayName : "Internal",
                Status = p.Status,
                BillingMethod = p.BillingMethod,
                BudgetAmount = p.BudgetAmount,
                Revenue = (from l in legs
                           join a in accounts on l.AccountId equals a.AccountId
                           where l.ProjectId == p.ProjectId && a.AccountTypeId == ProjectAccountTypes.Income
                           select l.CreditAmountBase - l.DebitAmountBase).Sum(),
                Cost = (from l in legs
                        join a in accounts on l.AccountId equals a.AccountId
                        where l.ProjectId == p.ProjectId && a.AccountTypeId == ProjectAccountTypes.Expense
                        select l.DebitAmountBase - l.CreditAmountBase).Sum(),
            };

        return rows.Select(r => new ProjectProfitabilityRow
        {
            ProjectId = r.ProjectId,
            ProjectCode = r.ProjectCode,
            ProjectName = r.ProjectName,
            Customer = r.Customer,
            Status = r.Status,
            BillingMethod = r.BillingMethod,
            BudgetAmount = r.BudgetAmount,
            Revenue = r.Revenue,
            Cost = r.Cost,
            Profit = r.Revenue - r.Cost,
            MarginPercent = r.Revenue != 0m ? Math.Round((r.Revenue - r.Cost) / r.Revenue * 100m, 2) : null,
        });
    }

    /// <summary>
    /// One row per project: its cost budget against the expense rows posted to
    /// it, and its tasks' budgeted hours against the hours logged. Over the
    /// project's whole life, because a budget is for the job rather than a period.
    /// </summary>
    public static IQueryable<ProjectBudgetRow> BudgetAgainstActual(
        IQueryable<ProjectRead> projects,
        IQueryable<ProjectTaskRead> tasks,
        IQueryable<TimeEntryRead> time,
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<AccountRead> accounts,
        DateOnly? asOf = null)
    {
        IQueryable<JournalLedgerRead> filteredLedger = ledger.Where(l =>
            l.ProjectId != null
            && (asOf == null || l.LedgerDate <= asOf));
        IQueryable<TimeEntryRead> filteredTime = time.Where(t =>
            asOf == null || t.WorkDate <= asOf);

        IQueryable<ProjectBudgetRow> rows =
            from p in projects
            select new ProjectBudgetRow
            {
                ProjectId = p.ProjectId,
                ProjectCode = p.ProjectCode,
                ProjectName = p.ProjectName,
                Status = p.Status,
                BudgetAmount = p.BudgetAmount,
                ActualCost = (from l in filteredLedger
                              join a in accounts on l.AccountId equals a.AccountId
                              where l.ProjectId == p.ProjectId && a.AccountTypeId == ProjectAccountTypes.Expense
                              select l.DebitAmountBase - l.CreditAmountBase).Sum(),
                BudgetHours = tasks.Where(k => k.ProjectId == p.ProjectId && k.BudgetHours != null).Sum(k => k.BudgetHours),
                ActualHours = filteredTime.Where(t => t.ProjectId == p.ProjectId).Sum(t => t.Hours),
            };

        return rows.Select(r => new ProjectBudgetRow
        {
            ProjectId = r.ProjectId,
            ProjectCode = r.ProjectCode,
            ProjectName = r.ProjectName,
            Status = r.Status,
            BudgetAmount = r.BudgetAmount,
            ActualCost = r.ActualCost,
            CostVariance = r.BudgetAmount != null ? r.BudgetAmount - r.ActualCost : null,
            CostUsedPercent = r.BudgetAmount != null && r.BudgetAmount > 0m
                ? Math.Round(r.ActualCost / r.BudgetAmount.Value * 100m, 2)
                : null,
            BudgetHours = r.BudgetHours,
            ActualHours = r.ActualHours,
            HoursVariance = r.BudgetHours != null ? r.BudgetHours - r.ActualHours : null,
        });
    }

    /// <summary>
    /// One row per project with anything waiting to be billed, valued the way
    /// the invoice's <b>Add project items</b> dialog values it (TK-106): hours at
    /// the project's, the task's or the person's rate; posted spend-money and
    /// bill lines marked billable, with their markup; uninvoiced milestones.
    /// </summary>
    public static IQueryable<ProjectUnbilledRow> Unbilled(
        IQueryable<ProjectRead> projects,
        IQueryable<ContactRead> contacts,
        IQueryable<ProjectTaskRead> tasks,
        IQueryable<ProjectMemberRead> members,
        IQueryable<TimeEntryRead> time,
        IQueryable<SpendMoneyDetailRead> spendLines,
        IQueryable<SpendMoneyRead> spend,
        IQueryable<BillDetailRead> billLines,
        IQueryable<BillRead> bills,
        IQueryable<ProjectMilestoneRead> milestones)
    {
        IQueryable<ProjectUnbilledRow> rows =
            from p in projects
            join c in contacts on p.ContactId equals c.ContactId into gc
            from c in gc.DefaultIfEmpty()
            select new ProjectUnbilledRow
            {
                ProjectId = p.ProjectId,
                ProjectCode = p.ProjectCode,
                ProjectName = p.ProjectName,
                Customer = c != null ? c.DisplayName : "Internal",
                BillingMethod = p.BillingMethod,
                UnbilledHours = time
                    .Where(t => t.ProjectId == p.ProjectId && t.IsBillable && t.BillingStatus == "Unbilled")
                    .Sum(t => t.Hours),
                TimeValue = (from t in time
                             where t.ProjectId == p.ProjectId && t.IsBillable && t.BillingStatus == "Unbilled"
                             join k in tasks on t.ProjectTaskId equals k.ProjectTaskId
                             join m in members on new { t.ProjectId, t.UserId } equals new { m.ProjectId, m.UserId } into gm
                             from m in gm.DefaultIfEmpty()
                             select Math.Round(
                                 t.Hours * (p.RateBasis == "TaskRate"
                                     ? k.HourlyRate ?? 0m
                                     : p.RateBasis == "UserRate"
                                         ? (m != null ? m.HourlyRate ?? 0m : 0m)
                                         : p.HourlyRate ?? 0m),
                                 2)).Sum(),
                Expenses = (from d in spendLines
                            join s in spend on d.SpendMoneyId equals s.SpendMoneyId
                            where d.ProjectId == p.ProjectId && d.IsBillable && d.BilledInvoiceId == null && s.Status == MoneyDocumentStatus.Posted
                            select Math.Round(d.AmountBase * (1m + ((d.MarkupPercent ?? 0m) / 100m)), 2)).Sum()
                    + (from l in billLines
                       join b in bills on l.BillId equals b.BillId
                       where l.ProjectId == p.ProjectId && l.IsBillable && l.BilledInvoiceId == null && b.Status == DocumentStatus.Posted
                       select Math.Round(Math.Round(l.TaxableAmount * b.ExchangeRate, 2) * (1m + ((l.MarkupPercent ?? 0m) / 100m)), 2)).Sum(),
                Milestones = milestones
                    .Where(m => m.ProjectId == p.ProjectId && m.InvoiceId == null)
                    .Sum(m => m.Amount),
            };

        return rows
            .Select(r => new ProjectUnbilledRow
            {
                ProjectId = r.ProjectId,
                ProjectCode = r.ProjectCode,
                ProjectName = r.ProjectName,
                Customer = r.Customer,
                BillingMethod = r.BillingMethod,
                UnbilledHours = r.UnbilledHours,
                TimeValue = r.TimeValue,
                Expenses = r.Expenses,
                Milestones = r.Milestones,
                Total = r.TimeValue + r.Expenses + r.Milestones,
            })
            .Where(r => r.UnbilledHours != 0m || r.Total != 0m);
    }

    /// <summary>
    /// Hours logged in the period, one row per project task and person, split
    /// into billable, billed and non-billable.
    /// </summary>
    public static IQueryable<ProjectTimeByUserRow> TimeByUser(
        IQueryable<ProjectRead> projects,
        IQueryable<ProjectTaskRead> tasks,
        IQueryable<TimeEntryRead> time,
        DateOnly? start,
        DateOnly? end) =>
        from t in time
        where (start == null || t.WorkDate >= start) && (end == null || t.WorkDate <= end)
        join p in projects on t.ProjectId equals p.ProjectId
        join k in tasks on t.ProjectTaskId equals k.ProjectTaskId
        group t by new { p.ProjectCode, p.ProjectName, k.ProjectTaskId, k.TaskName, t.UserId } into g
        select new ProjectTimeByUserRow
        {
            RowKey = g.Key.ProjectCode + "/" + g.Key.ProjectTaskId.ToString() + "/" + g.Key.UserId.ToString(),
            ProjectCode = g.Key.ProjectCode,
            ProjectName = g.Key.ProjectName,
            TaskName = g.Key.TaskName,
            UserId = g.Key.UserId,
            Hours = g.Sum(x => x.Hours),
            BillableHours = g.Sum(x => x.IsBillable ? x.Hours : 0m),
            BilledHours = g.Sum(x => x.BillingStatus == "Billed" ? x.Hours : 0m),
            NonBillableHours = g.Sum(x => x.IsBillable ? 0m : x.Hours),
        };
}

public sealed class ProjectProfitabilityRow
{
    public long ProjectId { get; set; }
    public string ProjectCode { get; set; } = null!;
    public string ProjectName { get; set; } = null!;
    public string Customer { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string BillingMethod { get; set; } = null!;
    public decimal? BudgetAmount { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal? MarginPercent { get; set; }
}

public sealed class ProjectBudgetRow
{
    public long ProjectId { get; set; }
    public string ProjectCode { get; set; } = null!;
    public string ProjectName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal? BudgetAmount { get; set; }
    public decimal ActualCost { get; set; }
    public decimal? CostVariance { get; set; }
    public decimal? CostUsedPercent { get; set; }
    public decimal? BudgetHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal? HoursVariance { get; set; }
}

public sealed class ProjectUnbilledRow
{
    public long ProjectId { get; set; }
    public string ProjectCode { get; set; } = null!;
    public string ProjectName { get; set; } = null!;
    public string Customer { get; set; } = null!;
    public string BillingMethod { get; set; } = null!;
    public decimal UnbilledHours { get; set; }
    public decimal TimeValue { get; set; }
    public decimal Expenses { get; set; }
    public decimal Milestones { get; set; }
    public decimal Total { get; set; }
}

public sealed class ProjectTimeByUserRow
{
    public string RowKey { get; set; } = null!;
    public string ProjectCode { get; set; } = null!;
    public string ProjectName { get; set; } = null!;
    public string TaskName { get; set; } = null!;
    public Guid UserId { get; set; }

    /// <summary>Filled from Master after the page is read; never in SQL, so neither filterable nor sortable.</summary>
    public string? UserName { get; set; }

    public decimal Hours { get; set; }
    public decimal BillableHours { get; set; }
    public decimal BilledHours { get; set; }
    public decimal NonBillableHours { get; set; }
}

public sealed class ProjectProfitabilitySource : ReportSource<ProjectProfitabilityRow>
{
    public override string ReportKey => "project-profitability";
    public override string Title => "Project Profitability";
    public override ReportModule Module => ReportModule.Projects;
    public override string RequiredPermission => "projects.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ProjectProfitabilityRow, string>("projectCode", ColumnDataType.Text, r => r.ProjectCode),
        ReportColumn.Of<ProjectProfitabilityRow, string>("projectName", ColumnDataType.Text, r => r.ProjectName),
        ReportColumn.Of<ProjectProfitabilityRow, string>("customer", ColumnDataType.Text, r => r.Customer, groupable: true),
        ReportColumn.Of<ProjectProfitabilityRow, string>("status", ColumnDataType.Text, r => r.Status, groupable: true),
        ReportColumn.Of<ProjectProfitabilityRow, string>("billingMethod", ColumnDataType.Text, r => r.BillingMethod, groupable: true),
        ReportColumn.Of<ProjectProfitabilityRow, decimal>("revenue", ColumnDataType.Money, r => r.Revenue, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectProfitabilityRow, decimal>("cost", ColumnDataType.Money, r => r.Cost, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectProfitabilityRow, decimal>("profit", ColumnDataType.Money, r => r.Profit, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectProfitabilityRow, decimal?>("marginPercent", ColumnDataType.Percent, r => r.MarginPercent),
        ReportColumn.Of<ProjectProfitabilityRow, decimal?>("budgetAmount", ColumnDataType.Money, r => r.BudgetAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectProfitabilityRow, long>("projectId", ColumnDataType.Number, r => r.ProjectId, filterable: false),
    ];

    protected override IQueryable<ProjectProfitabilityRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        ProjectReports.Profitability(db.Projects, db.Contacts, db.Ledger, db.Accounts, parameters.Date("from"), parameters.Date("to"));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ProjectProfitabilityRow, string>>)(r => r.ProjectCode);
}

public sealed class ProjectBudgetSource : ReportSource<ProjectBudgetRow>
{
    public override string ReportKey => "project-budget-vs-actual";
    public override string Title => "Project Budget vs Actual";
    public override ReportModule Module => ReportModule.Projects;
    public override string RequiredPermission => "projects.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "asOf", Label = "As at", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ProjectBudgetRow, string>("projectCode", ColumnDataType.Text, r => r.ProjectCode),
        ReportColumn.Of<ProjectBudgetRow, string>("projectName", ColumnDataType.Text, r => r.ProjectName),
        ReportColumn.Of<ProjectBudgetRow, string>("status", ColumnDataType.Text, r => r.Status, groupable: true),
        ReportColumn.Of<ProjectBudgetRow, decimal?>("budgetAmount", ColumnDataType.Money, r => r.BudgetAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectBudgetRow, decimal>("actualCost", ColumnDataType.Money, r => r.ActualCost, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectBudgetRow, decimal?>("costVariance", ColumnDataType.Money, r => r.CostVariance, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectBudgetRow, decimal?>("costUsedPercent", ColumnDataType.Percent, r => r.CostUsedPercent),
        ReportColumn.Of<ProjectBudgetRow, decimal?>("budgetHours", ColumnDataType.Quantity, r => r.BudgetHours, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectBudgetRow, decimal>("actualHours", ColumnDataType.Quantity, r => r.ActualHours, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectBudgetRow, decimal?>("hoursVariance", ColumnDataType.Quantity, r => r.HoursVariance, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectBudgetRow, long>("projectId", ColumnDataType.Number, r => r.ProjectId, filterable: false),
    ];

    protected override IQueryable<ProjectBudgetRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        ProjectReports.BudgetAgainstActual(db.Projects, db.ProjectTasks, db.TimeEntries, db.Ledger, db.Accounts, parameters.Date("asOf"));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ProjectBudgetRow, string>>)(r => r.ProjectCode);
}

public sealed class ProjectUnbilledSource : ReportSource<ProjectUnbilledRow>
{
    public override string ReportKey => "project-unbilled-work";
    public override string Title => "Project Unbilled Work";
    public override ReportModule Module => ReportModule.Projects;
    public override string RequiredPermission => "projects.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ProjectUnbilledRow, string>("projectCode", ColumnDataType.Text, r => r.ProjectCode),
        ReportColumn.Of<ProjectUnbilledRow, string>("projectName", ColumnDataType.Text, r => r.ProjectName),
        ReportColumn.Of<ProjectUnbilledRow, string>("customer", ColumnDataType.Text, r => r.Customer, groupable: true),
        ReportColumn.Of<ProjectUnbilledRow, string>("billingMethod", ColumnDataType.Text, r => r.BillingMethod, groupable: true),
        ReportColumn.Of<ProjectUnbilledRow, decimal>("unbilledHours", ColumnDataType.Quantity, r => r.UnbilledHours, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectUnbilledRow, decimal>("timeValue", ColumnDataType.Money, r => r.TimeValue, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectUnbilledRow, decimal>("expenses", ColumnDataType.Money, r => r.Expenses, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectUnbilledRow, decimal>("milestones", ColumnDataType.Money, r => r.Milestones, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectUnbilledRow, decimal>("total", ColumnDataType.Money, r => r.Total, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectUnbilledRow, long>("projectId", ColumnDataType.Number, r => r.ProjectId, filterable: false),
    ];

    protected override IQueryable<ProjectUnbilledRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        ProjectReports.Unbilled(
            db.Projects,
            db.Contacts,
            db.ProjectTasks,
            db.ProjectMembers,
            db.TimeEntries,
            db.SpendMoneyDetails,
            db.SpendMoney,
            db.BillDetails,
            db.Bills,
            db.ProjectMilestones);

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ProjectUnbilledRow, string>>)(r => r.ProjectCode);
}

public sealed class ProjectTimeByUserSource : ReportSource<ProjectTimeByUserRow>
{
    private readonly BatchedNameResolverRepository _resolver;

    public ProjectTimeByUserSource(BatchedNameResolverRepository resolver) => _resolver = resolver;

    public override string ReportKey => "project-time-by-user";
    public override string Title => "Project Time by User";
    public override ReportModule Module => ReportModule.Projects;
    public override string RequiredPermission => "projects.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ProjectTimeByUserRow, string>("projectCode", ColumnDataType.Text, r => r.ProjectCode, groupable: true),
        ReportColumn.Of<ProjectTimeByUserRow, string>("projectName", ColumnDataType.Text, r => r.ProjectName, groupable: true),
        ReportColumn.Of<ProjectTimeByUserRow, string>("taskName", ColumnDataType.Text, r => r.TaskName, groupable: true),
        ReportColumn.Of<ProjectTimeByUserRow, string?>("userName", ColumnDataType.Text, r => r.UserName, sortable: false, filterable: false),
        ReportColumn.Of<ProjectTimeByUserRow, decimal>("hours", ColumnDataType.Quantity, r => r.Hours, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectTimeByUserRow, decimal>("billableHours", ColumnDataType.Quantity, r => r.BillableHours, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectTimeByUserRow, decimal>("billedHours", ColumnDataType.Quantity, r => r.BilledHours, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ProjectTimeByUserRow, decimal>("nonBillableHours", ColumnDataType.Quantity, r => r.NonBillableHours, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<ProjectTimeByUserRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        ProjectReports.TimeByUser(db.Projects, db.ProjectTasks, db.TimeEntries, parameters.Date("from"), parameters.Date("to"));

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ProjectTimeByUserRow, string>>)(r => r.RowKey);

    protected override async Task FormatRowsAsync(IReadOnlyList<ProjectTimeByUserRow> page, CancellationToken ct)
    {
        Dictionary<Guid, string> names = await _resolver.GetUserNamesAsync(page.Select(r => (Guid?)r.UserId), ct);

        foreach (ProjectTimeByUserRow row in page)
        {
            row.UserName = names.GetValueOrDefault(row.UserId);
        }
    }
}

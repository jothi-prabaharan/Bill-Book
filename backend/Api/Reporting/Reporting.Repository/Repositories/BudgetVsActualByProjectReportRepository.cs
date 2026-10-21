using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using static Reporting.Repository.Repositories.BatchedNameResolverRepository;

namespace Reporting.Repository.Repositories;

/// <summary>
/// Budget vs Actual by Project (TK-207): a budget kept by project against the
/// postings that carry each project, for whole months of its year, one block
/// per project, and the profit across the projects shown.
///
/// <b>Each project is laid out by Budget vs Actual's own composition</b>
/// (<see cref="BudgetVsActualReportRepository.Compose"/>), over that project's
/// sums alone, so a project's sections, gross and net profit and every variance
/// mean exactly what they mean on Budget vs Actual (TK-204), and the window is
/// the same months. A posting with no project, and a budget line with none,
/// belong to no project and are not here: they are on Budget vs Actual.
///
/// <b>Budgets and actuals are summed in the database</b>, one row per project
/// and account each. An account made inactive since it moved is still shown,
/// as on Budget vs Actual, and so is a project since completed or cancelled.
/// </summary>
public sealed class BudgetVsActualByProjectReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _master;

    public BudgetVsActualByProjectReportRepository(ReportingDbContext db, BatchedNameResolverRepository master)
    {
        _db = db;
        _master = master;
    }

    /// <summary>The branch's projects, by code, for the project filter.</summary>
    public async Task<IReadOnlyList<BudgetVsActualProjectOptionDto>> ProjectsAsync(CancellationToken ct) =>
        [.. (await _db.Projects.OrderBy(p => p.ProjectCode).ToListAsync(ct)).Select(ToDto)];

    /// <summary>One of the branch's projects, or null when it is not the branch's.</summary>
    public async Task<BudgetVsActualProjectOptionDto?> FindProjectAsync(long projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId, ct) is ProjectRead project ? ToDto(project) : null;

    /// <summary>Why the budget cannot be used here, or null. It must be kept by project. Pure.</summary>
    public static string? BudgetRefusal(BudgetVsActualBudgetDto budget) =>
        budget.ByProject ? null : "This budget is not kept by project. Choose one that is, or run Budget vs Actual.";

    /// <summary>The report for a budget and a request the controller has already validated.</summary>
    public async Task<BudgetVsActualByProjectReportResultDto> RunAsync(
        BudgetVsActualBudgetDto budget, BudgetVsActualByProjectReportRequest request, CancellationToken ct)
    {
        DateOnly asOf = request.AsOfDate!.Value;
        BudgetPeriod period = request.Period!.Value;
        BudgetWindow window = BudgetVsActualReportRepository.WindowOf(budget, asOf, period);

        List<AccountTypeDto> types = await _master.GetAccountTypesFullAsync(ct);
        List<ProfitAndLossAccount> accounts = await BudgetVsActualReportRepository.AccountsOf(_db.Accounts).ToListAsync(ct);
        List<BudgetVsActualProjectOptionDto> projects = [.. await ProjectsAsync(ct)];

        List<ProjectAccountAmount> budgeted = await Budgeted(
            _db.BudgetLines, budget.BudgetId, window.PeriodFrom, window.PeriodTo, request.ProjectId).ToListAsync(ct);
        List<ProjectAccountAmount> actuals = await Actuals(_db.Ledger, window.From, window.To, request.ProjectId).ToListAsync(ct);

        return Compose(accounts, types, projects, budgeted, actuals, budget, asOf, period, window, request.ProjectId);
    }

    /// <summary>Each project and account's budget in the months covered, as entered; lines with no project left out. LINQ only.</summary>
    public static IQueryable<ProjectAccountAmount> Budgeted(
        IQueryable<BudgetLineRead> lines, long budgetId, int periodFrom, int periodTo, long? projectId)
    {
        IQueryable<BudgetLineRead> kept = lines
            .Where(l => l.BudgetId == budgetId && l.ProjectId != null && l.Period >= periodFrom && l.Period <= periodTo);

        if (projectId is long only)
        {
            kept = kept.Where(l => l.ProjectId == only);
        }

        return kept
            .GroupBy(l => new { ProjectId = l.ProjectId!.Value, l.AccountId })
            .Select(g => new ProjectAccountAmount { ProjectId = g.Key.ProjectId, AccountId = g.Key.AccountId, Amount = g.Sum(l => l.Amount) });
    }

    /// <summary>Each project and account's debits less credits between two dates; postings with no project left out. LINQ only.</summary>
    public static IQueryable<ProjectAccountAmount> Actuals(
        IQueryable<JournalLedgerRead> ledger, DateOnly from, DateOnly to, long? projectId)
    {
        IQueryable<JournalLedgerRead> kept = ledger
            .Where(l => l.ProjectId != null && l.LedgerDate >= from && l.LedgerDate <= to);

        if (projectId is long only)
        {
            kept = kept.Where(l => l.ProjectId == only);
        }

        return kept
            .GroupBy(l => new { ProjectId = l.ProjectId!.Value, l.AccountId })
            .Select(g => new ProjectAccountAmount
            {
                ProjectId = g.Key.ProjectId,
                AccountId = g.Key.AccountId,
                Amount = g.Sum(l => l.DebitAmountBase - l.CreditAmountBase),
            });
    }

    /// <summary>
    /// The report from the accounts, the projects, the sums by project and
    /// account, and the account types. Pure.
    ///
    /// Each project with a budget or an actual on an income or expense account
    /// is composed by Budget vs Actual's own rules over its sums alone; the
    /// profit across the projects is composed the same way over their sums
    /// added together, so it is the projects' profits added up.
    /// </summary>
    public static BudgetVsActualByProjectReportResultDto Compose(
        IReadOnlyList<ProfitAndLossAccount> accounts,
        IReadOnlyList<AccountTypeDto> types,
        IReadOnlyList<BudgetVsActualProjectOptionDto> projects,
        IReadOnlyList<ProjectAccountAmount> budgeted,
        IReadOnlyList<ProjectAccountAmount> actuals,
        BudgetVsActualBudgetDto budget,
        DateOnly asOf,
        BudgetPeriod period,
        BudgetWindow window,
        long? projectId)
    {
        Dictionary<long, BudgetVsActualProjectOptionDto> projectById = projects.ToDictionary(p => p.ProjectId);
        List<BudgetVsActualProjectDto> shown = [];
        List<ProjectAccountAmount> shownBudgets = [];
        List<ProjectAccountAmount> shownActuals = [];

        IEnumerable<long> ids = budgeted.Select(b => b.ProjectId).Concat(actuals.Select(a => a.ProjectId)).Distinct();
        foreach (long id in ids)
        {
            List<ProjectAccountAmount> itsBudgets = [.. budgeted.Where(b => b.ProjectId == id)];
            List<ProjectAccountAmount> itsActuals = [.. actuals.Where(a => a.ProjectId == id)];
            BudgetVsActualReportResultDto composed = BudgetVsActualReportRepository.Compose(
                accounts, types, Amounts(itsBudgets), Amounts(itsActuals), budget, asOf, period, window);

            // Only balance-sheet lines, or figures that net to nothing: nothing to show.
            if (composed.Sections.All(s => s.Lines.Count == 0))
            {
                continue;
            }

            BudgetVsActualProjectOptionDto project = projectById.GetValueOrDefault(id)
                ?? new BudgetVsActualProjectOptionDto { ProjectId = id, ProjectName = $"Project {id}", Status = ProjectStatus.Active };

            shown.Add(new BudgetVsActualProjectDto
            {
                ProjectId = id,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                Status = project.Status,
                Sections = composed.Sections,
                GrossProfit = composed.GrossProfit,
                NetProfit = composed.NetProfit,
            });
            shownBudgets.AddRange(itsBudgets);
            shownActuals.AddRange(itsActuals);
        }

        BudgetVsActualReportResultDto total = BudgetVsActualReportRepository.Compose(
            accounts, types, Amounts(shownBudgets), Amounts(shownActuals), budget, asOf, period, window);

        return new BudgetVsActualByProjectReportResultDto
        {
            Budget = budget,
            AsOfDate = asOf,
            Period = period,
            PeriodFrom = window.PeriodFrom,
            PeriodTo = window.PeriodTo,
            FromDate = window.From,
            ToDate = window.To,
            ProjectId = projectId,
            Projects = [.. shown.OrderBy(p => p.ProjectCode, StringComparer.Ordinal).ThenBy(p => p.ProjectId)],
            GrossProfit = total.GrossProfit,
            NetProfit = total.NetProfit,
        };
    }

    /// <summary>Sums by project and account added up by account, for Budget vs Actual's composition.</summary>
    private static List<AccountAmount> Amounts(IEnumerable<ProjectAccountAmount> amounts) =>
        [.. amounts.GroupBy(a => a.AccountId).Select(g => new AccountAmount { AccountId = g.Key, Amount = g.Sum(a => a.Amount) })];

    private static BudgetVsActualProjectOptionDto ToDto(ProjectRead project) => new()
    {
        ProjectId = project.ProjectId,
        ProjectCode = project.ProjectCode,
        ProjectName = project.ProjectName,
        Status = Enum.TryParse(project.Status, out ProjectStatus status) ? status : ProjectStatus.Active,
    };
}

/// <summary>One project and account's summed amount: a budget as entered, or debits less credits.</summary>
public sealed class ProjectAccountAmount
{
    public long ProjectId { get; set; }
    public long AccountId { get; set; }
    public decimal Amount { get; set; }
}

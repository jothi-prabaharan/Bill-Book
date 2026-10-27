using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

/// <summary>
/// Intermediate grouped sum of actual costs per project.
/// </summary>
public sealed class ProjectCostSum
{
    public long ProjectId { get; set; }
    public decimal ActualCost { get; set; }
}

/// <summary>
/// Intermediate grouped sum of logged hours per project task.
/// </summary>
public sealed class TaskHoursSum
{
    public long ProjectId { get; set; }
    public long ProjectTaskId { get; set; }
    public decimal ActualHours { get; set; }
}

/// <summary>
/// Repository for Project Budget vs Actual report (TK-257):
/// Compares each project's cost budget and tasks' hour budgets against actual ledger costs and logged time entries up to an as-at date.
/// </summary>
public sealed class ProjectBudgetVsActualReportRepository
{
    public const int ExpenseAccountType = 5;

    private readonly ReportingDbContext _db;

    public ProjectBudgetVsActualReportRepository(ReportingDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Pure validation for AsOfDate.
    /// </summary>
    public static string? AsOfDateRefusal(DateOnly? asOfDate) =>
        asOfDate is null ? "As of date is required." : null;

    /// <summary>
    /// Pure validation for customer belonging to branch.
    /// </summary>
    public static string? CustomerRefusal(ContactRead? customer) =>
        customer is null ? "That customer is not on this branch." : null;

    /// <summary>
    /// Pure validation for project belonging to branch.
    /// </summary>
    public static string? ProjectRefusal(ProjectRead? project) =>
        project is null ? "That project is not on this branch." : null;

    /// <summary>
    /// Finds a customer contact on the branch.
    /// </summary>
    public async Task<ContactRead?> FindCustomerAsync(long customerId, CancellationToken ct) =>
        await _db.Contacts.FirstOrDefaultAsync(c => c.ContactId == customerId, ct);

    /// <summary>
    /// Finds a project on the branch.
    /// </summary>
    public async Task<ProjectRead?> FindProjectAsync(long projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId, ct);

    /// <summary>
    /// Lookups for filter selectors.
    /// </summary>
    public async Task<ProjectBudgetLookupsResponse> GetLookupsAsync(CancellationToken ct)
    {
        List<ProjectLookupItemDto> customers = await _db.Contacts
            .Where(c => c.IsCustomer)
            .OrderBy(c => c.DisplayName)
            .Select(c => new ProjectLookupItemDto
            {
                Id = c.ContactId,
                Code = c.ContactCode ?? string.Empty,
                Name = c.DisplayName
            })
            .ToListAsync(ct);

        List<string> statuses = await _db.Projects
            .Select(p => p.Status)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

        if (statuses.Count == 0)
        {
            statuses = ["Active", "Completed", "OnHold", "Cancelled"];
        }

        List<ProjectLookupItemDto> projects = await _db.Projects
            .OrderBy(p => p.ProjectCode)
            .Select(p => new ProjectLookupItemDto
            {
                Id = p.ProjectId,
                Code = p.ProjectCode,
                Name = p.ProjectName
            })
            .ToListAsync(ct);

        return new ProjectBudgetLookupsResponse
        {
            Customers = customers,
            Statuses = statuses,
            Projects = projects
        };
    }

    /// <summary>
    /// Executes the report query up to the as-at date and composes the result.
    /// </summary>
    public async Task<ProjectBudgetVsActualReportResponse> RunAsync(
        ProjectBudgetVsActualReportRequest request,
        CancellationToken ct)
    {
        DateOnly asOf = request.AsOfDate!.Value;

        List<ProjectRead> projects = await _db.Projects.ToListAsync(ct);
        List<ContactRead> contacts = await _db.Contacts.ToListAsync(ct);
        List<ProjectTaskRead> tasks = await _db.ProjectTasks.ToListAsync(ct);

        List<ProjectCostSum> costSums = await ProjectActualCosts(
            _db.Ledger,
            _db.Accounts,
            asOf,
            request.ProjectId).ToListAsync(ct);

        List<TaskHoursSum> hourSums = await TaskActualHours(
            _db.TimeEntries,
            asOf,
            request.ProjectId).ToListAsync(ct);

        return Compose(projects, contacts, tasks, costSums, hourSums, request);
    }

    /// <summary>
    /// Static LINQ builder summing project costs on expense accounts up to the as-at date.
    /// Pure LINQ, no correlated subquery, zero number-to-text comparisons.
    /// </summary>
    public static IQueryable<ProjectCostSum> ProjectActualCosts(
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<AccountRead> accounts,
        DateOnly asOf,
        long? projectId = null)
    {
        IQueryable<JournalLedgerRead> filtered = ledger.Where(l =>
            l.ProjectId != null
            && l.LedgerDate <= asOf);

        if (projectId.HasValue)
        {
            filtered = filtered.Where(l => l.ProjectId == projectId.Value);
        }

        return from l in filtered
               join a in accounts on l.AccountId equals a.AccountId
               where a.AccountTypeId == ExpenseAccountType
               group l by l.ProjectId!.Value into g
               select new ProjectCostSum
               {
                   ProjectId = g.Key,
                   ActualCost = g.Sum(x => x.DebitAmountBase - x.CreditAmountBase)
               };
    }

    /// <summary>
    /// Static LINQ builder summing logged hours per project task up to the as-at date.
    /// Pure LINQ, no correlated subquery, zero number-to-text comparisons.
    /// </summary>
    public static IQueryable<TaskHoursSum> TaskActualHours(
        IQueryable<TimeEntryRead> time,
        DateOnly asOf,
        long? projectId = null)
    {
        IQueryable<TimeEntryRead> filtered = time.Where(t => t.WorkDate <= asOf);

        if (projectId.HasValue)
        {
            filtered = filtered.Where(t => t.ProjectId == projectId.Value);
        }

        return from t in filtered
               group t by new { t.ProjectId, t.ProjectTaskId } into g
               select new TaskHoursSum
               {
                   ProjectId = g.Key.ProjectId,
                   ProjectTaskId = g.Key.ProjectTaskId,
                   ActualHours = g.Sum(x => x.Hours)
               };
    }

    /// <summary>
    /// Pure composition function for Project Budget vs Actual report.
    /// Computes variances, % used, over-budget indicators, task hierarchies, and summary metrics.
    /// </summary>
    public static ProjectBudgetVsActualReportResponse Compose(
        IReadOnlyList<ProjectRead> projects,
        IReadOnlyList<ContactRead> contacts,
        IReadOnlyList<ProjectTaskRead> tasks,
        IReadOnlyList<ProjectCostSum> costSums,
        IReadOnlyList<TaskHoursSum> hourSums,
        ProjectBudgetVsActualReportRequest request)
    {
        Dictionary<long, ContactRead> contactById = contacts.ToDictionary(c => c.ContactId);
        Dictionary<long, decimal> actualCostByProject = costSums.ToDictionary(c => c.ProjectId, c => c.ActualCost);

        // Group actual hours by (ProjectId, ProjectTaskId)
        Dictionary<(long ProjectId, long TaskId), decimal> hoursByTask =
            hourSums.ToDictionary(h => (h.ProjectId, h.ProjectTaskId), h => h.ActualHours);

        IEnumerable<ProjectRead> filteredProjects = projects;

        if (request.ProjectId.HasValue)
        {
            filteredProjects = filteredProjects.Where(p => p.ProjectId == request.ProjectId.Value);
        }

        if (request.CustomerId.HasValue)
        {
            filteredProjects = filteredProjects.Where(p => p.ContactId == request.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            filteredProjects = filteredProjects.Where(p =>
                string.Equals(p.Status, request.Status, StringComparison.OrdinalIgnoreCase));
        }

        List<ProjectBudgetVsActualProjectDto> projectDtos = [];

        foreach (ProjectRead project in filteredProjects)
        {
            decimal actualCost = actualCostByProject.GetValueOrDefault(project.ProjectId, 0m);
            decimal? budgetAmount = project.BudgetAmount;
            decimal? costVariance = budgetAmount.HasValue ? budgetAmount.Value - actualCost : null;
            decimal? costUsedPercent = budgetAmount.HasValue && budgetAmount.Value > 0m
                ? Math.Round((actualCost / budgetAmount.Value) * 100m, 2)
                : null;
            bool isCostOverBudget = budgetAmount.HasValue && actualCost > budgetAmount.Value;

            string customerName = project.ContactId.HasValue && contactById.TryGetValue(project.ContactId.Value, out ContactRead? c)
                ? c.DisplayName
                : "Internal";

            // Compose tasks for this project
            List<ProjectTaskRead> projectTasks = [.. tasks.Where(t => t.ProjectId == project.ProjectId).OrderBy(t => t.TaskName, StringComparer.Ordinal)];
            List<ProjectTaskBudgetDto> taskDtos = [];

            decimal projectActualHours = 0m;
            decimal? projectBudgetHours = null;
            bool anyBudgetHours = false;
            decimal sumBudgetHours = 0m;

            foreach (ProjectTaskRead task in projectTasks)
            {
                decimal taskActualHours = hoursByTask.GetValueOrDefault((project.ProjectId, task.ProjectTaskId), 0m);
                projectActualHours += taskActualHours;

                decimal? taskBudgetHours = task.BudgetHours;
                if (taskBudgetHours.HasValue)
                {
                    anyBudgetHours = true;
                    sumBudgetHours += taskBudgetHours.Value;
                }

                decimal? taskHoursVariance = taskBudgetHours.HasValue ? taskBudgetHours.Value - taskActualHours : null;
                decimal? taskHoursUsedPercent = taskBudgetHours.HasValue && taskBudgetHours.Value > 0m
                    ? Math.Round((taskActualHours / taskBudgetHours.Value) * 100m, 2)
                    : null;
                bool isTaskOverBudget = taskBudgetHours.HasValue && taskActualHours > taskBudgetHours.Value;

                taskDtos.Add(new ProjectTaskBudgetDto
                {
                    ProjectTaskId = task.ProjectTaskId,
                    TaskName = task.TaskName,
                    BudgetHours = taskBudgetHours,
                    ActualHours = taskActualHours,
                    HoursVariance = taskHoursVariance,
                    HoursUsedPercent = taskHoursUsedPercent,
                    IsOverBudget = isTaskOverBudget
                });
            }

            if (anyBudgetHours)
            {
                projectBudgetHours = sumBudgetHours;
            }

            decimal? hoursVariance = projectBudgetHours.HasValue ? projectBudgetHours.Value - projectActualHours : null;
            decimal? hoursUsedPercent = projectBudgetHours.HasValue && projectBudgetHours.Value > 0m
                ? Math.Round((projectActualHours / projectBudgetHours.Value) * 100m, 2)
                : null;
            bool isHoursOverBudget = projectBudgetHours.HasValue && projectActualHours > projectBudgetHours.Value;

            projectDtos.Add(new ProjectBudgetVsActualProjectDto
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                CustomerId = project.ContactId,
                Customer = customerName,
                Status = project.Status,
                BudgetAmount = budgetAmount,
                ActualCost = actualCost,
                CostVariance = costVariance,
                CostUsedPercent = costUsedPercent,
                IsCostOverBudget = isCostOverBudget,
                BudgetHours = projectBudgetHours,
                ActualHours = projectActualHours,
                HoursVariance = hoursVariance,
                HoursUsedPercent = hoursUsedPercent,
                IsHoursOverBudget = isHoursOverBudget,
                Tasks = taskDtos
            });
        }

        projectDtos = [.. projectDtos.OrderBy(p => p.ProjectCode, StringComparer.Ordinal).ThenBy(p => p.ProjectName, StringComparer.Ordinal)];

        decimal totalBudgetAmount = projectDtos.Where(p => p.BudgetAmount.HasValue).Sum(p => p.BudgetAmount!.Value);
        decimal totalActualCost = projectDtos.Sum(p => p.ActualCost);
        decimal totalCostVariance = totalBudgetAmount - totalActualCost;
        decimal? totalCostUsedPercent = totalBudgetAmount > 0m
            ? Math.Round((totalActualCost / totalBudgetAmount) * 100m, 2)
            : null;

        decimal totalBudgetHours = projectDtos.Where(p => p.BudgetHours.HasValue).Sum(p => p.BudgetHours!.Value);
        decimal totalActualHours = projectDtos.Sum(p => p.ActualHours);
        decimal totalHoursVariance = totalBudgetHours - totalActualHours;
        decimal? totalHoursUsedPercent = totalBudgetHours > 0m
            ? Math.Round((totalActualHours / totalBudgetHours) * 100m, 2)
            : null;

        int overBudgetCount = projectDtos.Count(p => p.IsCostOverBudget || p.IsHoursOverBudget);

        ProjectBudgetVsActualSummaryDto summary = new()
        {
            TotalProjects = projectDtos.Count,
            TotalBudgetAmount = totalBudgetAmount,
            TotalActualCost = totalActualCost,
            TotalCostVariance = totalCostVariance,
            TotalCostUsedPercent = totalCostUsedPercent,
            TotalBudgetHours = totalBudgetHours,
            TotalActualHours = totalActualHours,
            TotalHoursVariance = totalHoursVariance,
            TotalHoursUsedPercent = totalHoursUsedPercent,
            OverBudgetProjectsCount = overBudgetCount
        };

        return new ProjectBudgetVsActualReportResponse
        {
            AsOfDate = request.AsOfDate!.Value,
            Projects = projectDtos,
            Summary = summary
        };
    }
}

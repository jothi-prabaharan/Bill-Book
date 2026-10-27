using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

/// <summary>
/// Intermediate grouped sum of debit and credit amounts for a project by account type.
/// </summary>
public sealed class ProjectAccountTypeSum
{
    public long ProjectId { get; set; }
    public int AccountTypeId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

/// <summary>
/// Repository for Project Profitability report (TK-256):
/// Evaluates income and expense ledger rows for projects, computing revenue, cost, profit, and margin against project budgets.
/// </summary>
public sealed class ProjectProfitabilityReportRepository
{
    public const int IncomeAccountType = 4;
    public const int ExpenseAccountType = 5;

    private readonly ReportingDbContext _db;

    public ProjectProfitabilityReportRepository(ReportingDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Pure validation for From and To dates.
    /// </summary>
    public static string? DateRefusal(DateOnly? from, DateOnly? to)
    {
        if (from is null || to is null)
        {
            return "From and To dates are both required.";
        }

        if (to < from)
        {
            return "To date must be on or after From date.";
        }

        return null;
    }

    /// <summary>
    /// Pure validation for customer belonging to the branch.
    /// </summary>
    public static string? CustomerRefusal(ContactRead? customer) =>
        customer is null ? "That customer is not on this branch." : null;

    /// <summary>
    /// Pure validation for project belonging to the branch.
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
    /// Fetches lookups for the filter dropdowns.
    /// </summary>
    public async Task<ProjectProfitabilityLookupsResponse> GetLookupsAsync(CancellationToken ct)
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

        List<string> billingMethods = await _db.Projects
            .Select(p => p.BillingMethod)
            .Where(b => !string.IsNullOrEmpty(b))
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(ct);

        if (billingMethods.Count == 0)
        {
            billingMethods = ["FixedFee", "TimeAndMaterials", "NonBillable"];
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

        return new ProjectProfitabilityLookupsResponse
        {
            Customers = customers,
            Statuses = statuses,
            BillingMethods = billingMethods,
            Projects = projects
        };
    }

    /// <summary>
    /// Executes the report query and composes the result.
    /// </summary>
    public async Task<ProjectProfitabilityReportResponse> RunAsync(
        ProjectProfitabilityReportRequest request,
        CancellationToken ct)
    {
        DateOnly from = request.From!.Value;
        DateOnly to = request.To!.Value;

        List<ProjectRead> projects = await _db.Projects.ToListAsync(ct);
        List<ContactRead> contacts = await _db.Contacts.ToListAsync(ct);

        List<ProjectAccountTypeSum> sums = await ProjectSumsByType(
            _db.Ledger,
            _db.Accounts,
            from,
            to,
            request.ProjectId).ToListAsync(ct);

        return Compose(projects, contacts, sums, request);
    }

    /// <summary>
    /// Static LINQ builder grouping ledger postings by project and account type (Income/Expense).
    /// Pure LINQ, no correlated subquery, zero number-to-text comparisons.
    /// </summary>
    public static IQueryable<ProjectAccountTypeSum> ProjectSumsByType(
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<AccountRead> accounts,
        DateOnly from,
        DateOnly to,
        long? projectId = null)
    {
        IQueryable<JournalLedgerRead> filteredLedger = ledger.Where(l =>
            l.ProjectId != null
            && l.LedgerDate >= from
            && l.LedgerDate <= to);

        if (projectId.HasValue)
        {
            filteredLedger = filteredLedger.Where(l => l.ProjectId == projectId.Value);
        }

        return from l in filteredLedger
               join a in accounts on l.AccountId equals a.AccountId
               where a.AccountTypeId == IncomeAccountType || a.AccountTypeId == ExpenseAccountType
               group l by new { ProjectId = l.ProjectId!.Value, a.AccountTypeId } into g
               select new ProjectAccountTypeSum
               {
                   ProjectId = g.Key.ProjectId,
                   AccountTypeId = g.Key.AccountTypeId,
                   Debit = g.Sum(x => x.DebitAmountBase),
                   Credit = g.Sum(x => x.CreditAmountBase)
               };
    }

    /// <summary>
    /// Pure composition function for Project Profitability report.
    /// Computes revenue (credit - debit for income), cost (debit - credit for expense), profit, margin, and summary totals.
    /// </summary>
    public static ProjectProfitabilityReportResponse Compose(
        IReadOnlyList<ProjectRead> projects,
        IReadOnlyList<ContactRead> contacts,
        IReadOnlyList<ProjectAccountTypeSum> sums,
        ProjectProfitabilityReportRequest request)
    {
        Dictionary<long, ContactRead> contactById = contacts.ToDictionary(c => c.ContactId);

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

        if (!string.IsNullOrWhiteSpace(request.BillingMethod))
        {
            filteredProjects = filteredProjects.Where(p =>
                string.Equals(p.BillingMethod, request.BillingMethod, StringComparison.OrdinalIgnoreCase));
        }

        List<ProjectProfitabilityRowDto> rows = [];

        foreach (ProjectRead project in filteredProjects)
        {
            ProjectAccountTypeSum? incomeSum = sums.FirstOrDefault(s =>
                s.ProjectId == project.ProjectId && s.AccountTypeId == IncomeAccountType);
            ProjectAccountTypeSum? expenseSum = sums.FirstOrDefault(s =>
                s.ProjectId == project.ProjectId && s.AccountTypeId == ExpenseAccountType);

            decimal revenue = (incomeSum?.Credit ?? 0m) - (incomeSum?.Debit ?? 0m);
            decimal cost = (expenseSum?.Debit ?? 0m) - (expenseSum?.Credit ?? 0m);
            decimal profit = revenue - cost;

            decimal? marginPercent = revenue != 0m
                ? Math.Round((profit / revenue) * 100m, 2)
                : null;

            string customerName = project.ContactId.HasValue && contactById.TryGetValue(project.ContactId.Value, out ContactRead? c)
                ? c.DisplayName
                : "Internal";

            decimal? budget = project.BudgetAmount;
            decimal? variance = budget.HasValue ? budget.Value - cost : null;

            rows.Add(new ProjectProfitabilityRowDto
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                CustomerId = project.ContactId,
                Customer = customerName,
                Status = project.Status,
                BillingMethod = project.BillingMethod,
                Revenue = revenue,
                Cost = cost,
                Profit = profit,
                MarginPercent = marginPercent,
                BudgetAmount = budget,
                BudgetVariance = variance
            });
        }

        rows = [.. rows.OrderBy(r => r.ProjectCode, StringComparer.Ordinal).ThenBy(r => r.ProjectName, StringComparer.Ordinal)];

        decimal totalRevenue = rows.Sum(r => r.Revenue);
        decimal totalCost = rows.Sum(r => r.Cost);
        decimal totalProfit = rows.Sum(r => r.Profit);
        decimal totalBudget = rows.Where(r => r.BudgetAmount.HasValue).Sum(r => r.BudgetAmount!.Value);
        decimal? overallMarginPercent = totalRevenue != 0m
            ? Math.Round((totalProfit / totalRevenue) * 100m, 2)
            : null;

        ProjectProfitabilitySummaryDto summary = new()
        {
            TotalProjects = rows.Count,
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            TotalProfit = totalProfit,
            OverallMarginPercent = overallMarginPercent,
            TotalBudgetAmount = totalBudget
        };

        return new ProjectProfitabilityReportResponse
        {
            From = request.From!.Value,
            To = request.To!.Value,
            Rows = rows,
            Summary = summary
        };
    }
}

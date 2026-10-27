using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;

namespace Reporting.Repository.Repositories;

/// <summary>
/// Intermediate unbilled time entry record for in-memory composition and testing.
/// </summary>
public sealed class UnbilledTimeDto
{
    public long ProjectId { get; set; }
    public long TimeEntryId { get; set; }
    public long ProjectTaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal Hours { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// Intermediate unbilled spend-money expense line for in-memory composition and testing.
/// </summary>
public sealed class UnbilledSpendDto
{
    public long ProjectId { get; set; }
    public long SpendMoneyDetailId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? TransactionNo { get; set; }
    public string? LineMemo { get; set; }
    public decimal AmountBase { get; set; }
    public decimal? MarkupPercent { get; set; }
    public decimal BillableAmount { get; set; }
}

/// <summary>
/// Intermediate unbilled vendor bill expense line for in-memory composition and testing.
/// </summary>
public sealed class UnbilledBillDto
{
    public long ProjectId { get; set; }
    public long BillDetailId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string? DocumentNo { get; set; }
    public string? Description { get; set; }
    public decimal TaxableAmountBase { get; set; }
    public decimal? MarkupPercent { get; set; }
    public decimal BillableAmount { get; set; }
}

/// <summary>
/// Intermediate unbilled milestone record for in-memory composition and testing.
/// </summary>
public sealed class UnbilledMilestoneDto
{
    public long ProjectId { get; set; }
    public long ProjectMilestoneId { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// Repository for Project Unbilled Work report (TK-258):
/// Calculates what could be invoiced now: billable hours, recharge expenses, and uninvoiced milestones by project up to an as-at date.
/// </summary>
public sealed class ProjectUnbilledWorkReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _nameResolver;

    public ProjectUnbilledWorkReportRepository(ReportingDbContext db, BatchedNameResolverRepository nameResolver)
    {
        _db = db;
        _nameResolver = nameResolver;
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
    /// Lookups for filter dropdowns.
    /// </summary>
    public async Task<ProjectUnbilledLookupsResponse> GetLookupsAsync(CancellationToken ct)
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

        List<ProjectLookupItemDto> projects = await _db.Projects
            .OrderBy(p => p.ProjectCode)
            .Select(p => new ProjectLookupItemDto
            {
                Id = p.ProjectId,
                Code = p.ProjectCode,
                Name = p.ProjectName
            })
            .ToListAsync(ct);

        return new ProjectUnbilledLookupsResponse
        {
            Customers = customers,
            Projects = projects
        };
    }

    /// <summary>
    /// Runs the report query, fetching intermediate projections and composing the final response.
    /// </summary>
    public async Task<ProjectUnbilledWorkReportResponse> RunAsync(
        ProjectUnbilledWorkReportRequest request,
        CancellationToken ct)
    {
        DateOnly asOf = request.AsOfDate!.Value;

        List<ProjectRead> projects = await _db.Projects.ToListAsync(ct);
        List<ContactRead> contacts = await _db.Contacts.ToListAsync(ct);

        List<UnbilledTimeDto> timeEntries = await QueryUnbilledTimeEntries(
            _db.TimeEntries,
            _db.Projects,
            _db.ProjectTasks,
            _db.ProjectMembers,
            asOf,
            request.ProjectId).ToListAsync(ct);

        List<UnbilledSpendDto> spendLines = await QueryUnbilledSpendLines(
            _db.SpendMoneyDetails,
            _db.SpendMoney,
            asOf,
            request.ProjectId).ToListAsync(ct);

        List<UnbilledBillDto> billLines = await QueryUnbilledBillLines(
            _db.BillDetails,
            _db.Bills,
            asOf,
            request.ProjectId).ToListAsync(ct);

        List<UnbilledMilestoneDto> milestones = await QueryUnbilledMilestones(
            _db.ProjectMilestones,
            request.ProjectId).ToListAsync(ct);

        // Resolve user display names for time entries
        Dictionary<Guid, string> userNames = await _nameResolver.GetUserNamesAsync(
            timeEntries.Select(t => (Guid?)t.UserId), ct);

        return Compose(projects, contacts, timeEntries, spendLines, billLines, milestones, userNames, request);
    }

    /// <summary>
    /// Static LINQ query for unbilled billable time entries up to asOf date.
    /// Pure LINQ, zero correlated subqueries, zero raw SQL.
    /// </summary>
    public static IQueryable<UnbilledTimeDto> QueryUnbilledTimeEntries(
        IQueryable<TimeEntryRead> time,
        IQueryable<ProjectRead> projects,
        IQueryable<ProjectTaskRead> tasks,
        IQueryable<ProjectMemberRead> members,
        DateOnly asOf,
        long? projectId = null)
    {
        IQueryable<TimeEntryRead> filteredTime = time.Where(t =>
            t.IsBillable
            && t.BillingStatus == "Unbilled"
            && t.WorkDate <= asOf);

        if (projectId.HasValue)
        {
            filteredTime = filteredTime.Where(t => t.ProjectId == projectId.Value);
        }

        return from t in filteredTime
               join p in projects on t.ProjectId equals p.ProjectId
               join k in tasks on t.ProjectTaskId equals k.ProjectTaskId
               join m in members on new { t.ProjectId, t.UserId } equals new { m.ProjectId, m.UserId } into gm
               from m in gm.DefaultIfEmpty()
               let rate = p.RateBasis == "TaskRate"
                   ? (k.HourlyRate ?? 0m)
                   : p.RateBasis == "UserRate"
                       ? (m != null ? (m.HourlyRate ?? 0m) : 0m)
                       : (p.HourlyRate ?? 0m)
               select new UnbilledTimeDto
               {
                   ProjectId = t.ProjectId,
                   TimeEntryId = t.TimeEntryId,
                   ProjectTaskId = t.ProjectTaskId,
                   TaskName = k.TaskName,
                   UserId = t.UserId,
                   WorkDate = t.WorkDate,
                   Hours = t.Hours,
                   Rate = rate,
                   Amount = Math.Round(t.Hours * rate, 2)
               };
    }

    /// <summary>
    /// Static LINQ query for unbilled posted spend-money lines up to asOf date.
    /// </summary>
    public static IQueryable<UnbilledSpendDto> QueryUnbilledSpendLines(
        IQueryable<SpendMoneyDetailRead> spendLines,
        IQueryable<SpendMoneyRead> spend,
        DateOnly asOf,
        long? projectId = null)
    {
        IQueryable<SpendMoneyDetailRead> filteredLines = spendLines.Where(d =>
            d.ProjectId != null
            && d.IsBillable
            && d.BilledInvoiceId == null);

        if (projectId.HasValue)
        {
            filteredLines = filteredLines.Where(d => d.ProjectId == projectId.Value);
        }

        return from d in filteredLines
               join s in spend on d.SpendMoneyId equals s.SpendMoneyId
               where s.Status == MoneyDocumentStatus.Posted && s.TransactionDate <= asOf
               let markup = d.MarkupPercent ?? 0m
               select new UnbilledSpendDto
               {
                   ProjectId = d.ProjectId!.Value,
                   SpendMoneyDetailId = d.SpendMoneyDetailId,
                   TransactionDate = s.TransactionDate,
                   TransactionNo = s.TransactionNo,
                   LineMemo = d.LineMemo,
                   AmountBase = d.AmountBase,
                   MarkupPercent = d.MarkupPercent,
                   BillableAmount = Math.Round(d.AmountBase * (1m + (markup / 100m)), 2)
               };
    }

    /// <summary>
    /// Static LINQ query for unbilled posted bill lines up to asOf date.
    /// </summary>
    public static IQueryable<UnbilledBillDto> QueryUnbilledBillLines(
        IQueryable<BillDetailRead> billLines,
        IQueryable<BillRead> bills,
        DateOnly asOf,
        long? projectId = null)
    {
        IQueryable<BillDetailRead> filteredLines = billLines.Where(l =>
            l.ProjectId != null
            && l.IsBillable
            && l.BilledInvoiceId == null);

        if (projectId.HasValue)
        {
            filteredLines = filteredLines.Where(l => l.ProjectId == projectId.Value);
        }

        return from l in filteredLines
               join b in bills on l.BillId equals b.BillId
               where b.Status == DocumentStatus.Posted && b.DocumentDate <= asOf
               let baseAmt = Math.Round(l.TaxableAmount * b.ExchangeRate, 2)
               let markup = l.MarkupPercent ?? 0m
               select new UnbilledBillDto
               {
                   ProjectId = l.ProjectId!.Value,
                   BillDetailId = l.BillDetailId,
                   DocumentDate = b.DocumentDate,
                   DocumentNo = b.DocumentNo,
                   Description = l.Description,
                   TaxableAmountBase = baseAmt,
                   MarkupPercent = l.MarkupPercent,
                   BillableAmount = Math.Round(baseAmt * (1m + (markup / 100m)), 2)
               };
    }

    /// <summary>
    /// Static LINQ query for uninvoiced milestones.
    /// </summary>
    public static IQueryable<UnbilledMilestoneDto> QueryUnbilledMilestones(
        IQueryable<ProjectMilestoneRead> milestones,
        long? projectId = null)
    {
        IQueryable<ProjectMilestoneRead> filtered = milestones.Where(m => m.InvoiceId == null);

        if (projectId.HasValue)
        {
            filtered = filtered.Where(m => m.ProjectId == projectId.Value);
        }

        return from m in filtered
               select new UnbilledMilestoneDto
               {
                   ProjectId = m.ProjectId,
                   ProjectMilestoneId = m.ProjectMilestoneId,
                   Amount = m.Amount
               };
    }

    /// <summary>
    /// Pure composition function for Project Unbilled Work report.
    /// Assembles hierarchical project cards and drill-down lines, filtering out projects with no unbilled work.
    /// </summary>
    public static ProjectUnbilledWorkReportResponse Compose(
        IReadOnlyList<ProjectRead> projects,
        IReadOnlyList<ContactRead> contacts,
        IReadOnlyList<UnbilledTimeDto> timeEntries,
        IReadOnlyList<UnbilledSpendDto> spendLines,
        IReadOnlyList<UnbilledBillDto> billLines,
        IReadOnlyList<UnbilledMilestoneDto> milestones,
        IReadOnlyDictionary<Guid, string> userNames,
        ProjectUnbilledWorkReportRequest request)
    {
        Dictionary<long, ContactRead> contactById = contacts.ToDictionary(c => c.ContactId);

        ILookup<long, UnbilledTimeDto> timeByProject = timeEntries.ToLookup(t => t.ProjectId);
        ILookup<long, UnbilledSpendDto> spendByProject = spendLines.ToLookup(s => s.ProjectId);
        ILookup<long, UnbilledBillDto> billByProject = billLines.ToLookup(b => b.ProjectId);
        ILookup<long, UnbilledMilestoneDto> milestonesByProject = milestones.ToLookup(m => m.ProjectId);

        IEnumerable<ProjectRead> filteredProjects = projects;

        if (request.ProjectId.HasValue)
        {
            filteredProjects = filteredProjects.Where(p => p.ProjectId == request.ProjectId.Value);
        }

        if (request.CustomerId.HasValue)
        {
            filteredProjects = filteredProjects.Where(p => p.ContactId == request.CustomerId.Value);
        }

        List<ProjectUnbilledProjectDto> projectDtos = [];

        foreach (ProjectRead p in filteredProjects.OrderBy(p => p.ProjectCode))
        {
            List<UnbilledTimeDto> projTime = timeByProject[p.ProjectId].ToList();
            List<UnbilledSpendDto> projSpend = spendByProject[p.ProjectId].ToList();
            List<UnbilledBillDto> projBill = billByProject[p.ProjectId].ToList();
            List<UnbilledMilestoneDto> projMilestones = milestonesByProject[p.ProjectId].ToList();

            decimal unbilledHours = projTime.Sum(t => t.Hours);
            decimal timeValue = projTime.Sum(t => t.Amount);
            decimal expenses = projSpend.Sum(s => s.BillableAmount) + projBill.Sum(b => b.BillableAmount);
            decimal milestoneValue = projMilestones.Sum(m => m.Amount);
            decimal total = timeValue + expenses + milestoneValue;

            // Projects with nothing waiting are left out unless filtered explicitly by ProjectId
            if (unbilledHours == 0m && total == 0m && !request.ProjectId.HasValue)
            {
                continue;
            }

            string customerName = p.ContactId.HasValue && contactById.TryGetValue(p.ContactId.Value, out ContactRead? contact)
                ? contact.DisplayName
                : "Internal";

            List<ProjectUnbilledLineDto> lines = [];

            // Add time lines
            foreach (UnbilledTimeDto t in projTime.OrderByDescending(t => t.WorkDate))
            {
                string personName = userNames.TryGetValue(t.UserId, out string? name) ? name : "Team Member";
                lines.Add(new ProjectUnbilledLineDto
                {
                    LineType = "Time",
                    Id = t.TimeEntryId,
                    Date = t.WorkDate,
                    Description = $"{personName} - {t.TaskName}",
                    Reference = t.TaskName,
                    Quantity = t.Hours,
                    UnitRate = t.Rate,
                    MarkupPercent = null,
                    Amount = t.Amount
                });
            }

            // Add spend money expense lines
            foreach (UnbilledSpendDto s in projSpend.OrderByDescending(s => s.TransactionDate))
            {
                lines.Add(new ProjectUnbilledLineDto
                {
                    LineType = "Expense",
                    Id = s.SpendMoneyDetailId,
                    Date = s.TransactionDate,
                    Description = !string.IsNullOrWhiteSpace(s.LineMemo) ? s.LineMemo : "Spend money recharge",
                    Reference = s.TransactionNo ?? "Spend Money",
                    Quantity = 1m,
                    UnitRate = s.AmountBase,
                    MarkupPercent = s.MarkupPercent,
                    Amount = s.BillableAmount
                });
            }

            // Add bill expense lines
            foreach (UnbilledBillDto b in projBill.OrderByDescending(b => b.DocumentDate))
            {
                lines.Add(new ProjectUnbilledLineDto
                {
                    LineType = "Expense",
                    Id = b.BillDetailId,
                    Date = b.DocumentDate,
                    Description = !string.IsNullOrWhiteSpace(b.Description) ? b.Description : "Bill line recharge",
                    Reference = b.DocumentNo ?? "Bill",
                    Quantity = 1m,
                    UnitRate = b.TaxableAmountBase,
                    MarkupPercent = b.MarkupPercent,
                    Amount = b.BillableAmount
                });
            }

            // Add milestone lines
            foreach (UnbilledMilestoneDto m in projMilestones)
            {
                lines.Add(new ProjectUnbilledLineDto
                {
                    LineType = "Milestone",
                    Id = m.ProjectMilestoneId,
                    Date = null,
                    Description = $"Milestone #{m.ProjectMilestoneId}",
                    Reference = "Milestone",
                    Quantity = 1m,
                    UnitRate = m.Amount,
                    MarkupPercent = null,
                    Amount = m.Amount
                });
            }

            projectDtos.Add(new ProjectUnbilledProjectDto
            {
                ProjectId = p.ProjectId,
                ProjectCode = p.ProjectCode,
                ProjectName = p.ProjectName,
                CustomerId = p.ContactId,
                Customer = customerName,
                BillingMethod = p.BillingMethod,
                RateBasis = p.RateBasis,
                HourlyRate = p.HourlyRate,
                UnbilledHours = unbilledHours,
                TimeValue = timeValue,
                Expenses = expenses,
                Milestones = milestoneValue,
                Total = total,
                LineCount = lines.Count,
                Lines = lines
            });
        }

        ProjectUnbilledSummaryDto summary = new()
        {
            TotalProjects = projectDtos.Count,
            TotalUnbilledHours = projectDtos.Sum(p => p.UnbilledHours),
            TotalTimeValue = projectDtos.Sum(p => p.TimeValue),
            TotalExpenses = projectDtos.Sum(p => p.Expenses),
            TotalMilestones = projectDtos.Sum(p => p.Milestones),
            GrandTotal = projectDtos.Sum(p => p.Total)
        };

        return new ProjectUnbilledWorkReportResponse
        {
            AsOfDate = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Projects = projectDtos,
            Summary = summary
        };
    }
}

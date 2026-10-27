using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

/// <summary>
/// Repository for Project Time by User report (TK-259):
/// Generates staff time reports grouped by user or project, splitting hours into billable, billed and non-billable with utilisation %.
/// </summary>
public sealed class ProjectTimeByUserReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _nameResolver;

    public ProjectTimeByUserReportRepository(ReportingDbContext db, BatchedNameResolverRepository nameResolver)
    {
        _db = db;
        _nameResolver = nameResolver;
    }

    /// <summary>
    /// Pure validation for FromDate and ToDate.
    /// </summary>
    public static string? DateRangeRefusal(DateOnly? from, DateOnly? to)
    {
        if (from is null) return "From date is required.";
        if (to is null) return "To date is required.";
        if (to < from) return "To date must not be earlier than from date.";
        return null;
    }

    /// <summary>
    /// Pure validation for project belonging to branch.
    /// </summary>
    public static string? ProjectRefusal(ProjectRead? project) =>
        project is null ? "That project is not on this branch." : null;

    /// <summary>
    /// Finds a project on the branch.
    /// </summary>
    public async Task<ProjectRead?> FindProjectAsync(long projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId, ct);

    /// <summary>
    /// Lookups for filter dropdowns: active project users and projects.
    /// </summary>
    public async Task<ProjectTimeByUserLookupsResponse> GetLookupsAsync(CancellationToken ct)
    {
        List<Guid> userIds = await _db.TimeEntries
            .Select(t => t.UserId)
            .Distinct()
            .ToListAsync(ct);

        Dictionary<Guid, string> userNames = await _nameResolver.GetUserNamesAsync(
            userIds.Select(id => (Guid?)id), ct);

        List<ProjectUserLookupItemDto> users = userIds
            .Select(id => new ProjectUserLookupItemDto
            {
                UserId = id,
                Name = userNames.TryGetValue(id, out string? name) ? name : "User " + id.ToString()[..8]
            })
            .OrderBy(u => u.Name)
            .ToList();

        List<ProjectLookupItemDto> projects = await _db.Projects
            .OrderBy(p => p.ProjectCode)
            .Select(p => new ProjectLookupItemDto
            {
                Id = p.ProjectId,
                Code = p.ProjectCode,
                Name = p.ProjectName
            })
            .ToListAsync(ct);

        return new ProjectTimeByUserLookupsResponse
        {
            Users = users,
            Projects = projects
        };
    }

    /// <summary>
    /// Runs the report query, resolves user names, and composes the final grouped response.
    /// </summary>
    public async Task<ProjectTimeByUserReportResponse> RunAsync(
        ProjectTimeByUserReportRequest request,
        CancellationToken ct)
    {
        DateOnly from = request.FromDate!.Value;
        DateOnly to = request.ToDate!.Value;

        List<ProjectTimeByUserRow> rows = await QueryTimeByUser(
            _db.TimeEntries,
            _db.Projects,
            _db.ProjectTasks,
            from,
            to,
            request.UserId,
            request.ProjectId).ToListAsync(ct);

        Dictionary<Guid, string> userNames = await _nameResolver.GetUserNamesAsync(
            rows.Select(r => (Guid?)r.UserId), ct);

        return Compose(rows, userNames, request);
    }

    /// <summary>
    /// Static LINQ query grouping logged hours by project code, task and user.
    /// Pure LINQ, zero correlated subqueries, zero raw SQL.
    /// </summary>
    public static IQueryable<ProjectTimeByUserRow> QueryTimeByUser(
        IQueryable<TimeEntryRead> time,
        IQueryable<ProjectRead> projects,
        IQueryable<ProjectTaskRead> tasks,
        DateOnly from,
        DateOnly to,
        Guid? userId = null,
        long? projectId = null)
    {
        IQueryable<TimeEntryRead> filteredTime = time.Where(t => t.WorkDate >= from && t.WorkDate <= to);

        if (userId.HasValue)
        {
            filteredTime = filteredTime.Where(t => t.UserId == userId.Value);
        }

        if (projectId.HasValue)
        {
            filteredTime = filteredTime.Where(t => t.ProjectId == projectId.Value);
        }

        return from t in filteredTime
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

    /// <summary>
    /// Pure composition function for Project Time by User report.
    /// Groups by user (or project), calculates utilisation %, and computes overall summary metrics.
    /// </summary>
    public static ProjectTimeByUserReportResponse Compose(
        IReadOnlyList<ProjectTimeByUserRow> rows,
        IReadOnlyDictionary<Guid, string> userNames,
        ProjectTimeByUserReportRequest request)
    {
        List<ProjectTimeItemDto> items = rows.Select(r =>
        {
            string userName = userNames.TryGetValue(r.UserId, out string? name) ? name : "Team Member";
            decimal? util = r.Hours > 0m ? Math.Round((r.BillableHours / r.Hours) * 100m, 2) : null;

            return new ProjectTimeItemDto
            {
                ProjectCode = r.ProjectCode,
                ProjectName = r.ProjectName,
                TaskName = r.TaskName,
                UserId = r.UserId,
                UserName = userName,
                Hours = r.Hours,
                BillableHours = r.BillableHours,
                BilledHours = r.BilledHours,
                NonBillableHours = r.NonBillableHours,
                UtilisationPercent = util
            };
        }).ToList();

        bool groupByProject = string.Equals(request.GroupBy, "project", StringComparison.OrdinalIgnoreCase);

        List<ProjectTimeGroupDto> groups = [];

        if (groupByProject)
        {
            var projectGroups = items
                .GroupBy(i => i.ProjectCode)
                .OrderBy(g => g.Key);

            foreach (var g in projectGroups)
            {
                string projectName = g.First().ProjectName;
                decimal totalHours = g.Sum(x => x.Hours);
                decimal billableHours = g.Sum(x => x.BillableHours);
                decimal billedHours = g.Sum(x => x.BilledHours);
                decimal nonBillableHours = g.Sum(x => x.NonBillableHours);
                decimal? util = totalHours > 0m ? Math.Round((billableHours / totalHours) * 100m, 2) : null;

                groups.Add(new ProjectTimeGroupDto
                {
                    GroupKey = g.Key,
                    GroupName = projectName,
                    GroupSubtext = g.Key,
                    TotalHours = totalHours,
                    TotalBillableHours = billableHours,
                    TotalBilledHours = billedHours,
                    TotalNonBillableHours = nonBillableHours,
                    TotalUtilisationPercent = util,
                    Items = g.OrderBy(x => x.UserName).ThenBy(x => x.TaskName).ToList()
                });
            }
        }
        else
        {
            var userGroups = items
                .GroupBy(i => i.UserId)
                .OrderBy(g => userNames.TryGetValue(g.Key, out string? n) ? n : g.Key.ToString());

            foreach (var g in userGroups)
            {
                string userName = userNames.TryGetValue(g.Key, out string? n) ? n : "Team Member";
                int distinctProjects = g.Select(x => x.ProjectCode).Distinct().Count();
                decimal totalHours = g.Sum(x => x.Hours);
                decimal billableHours = g.Sum(x => x.BillableHours);
                decimal billedHours = g.Sum(x => x.BilledHours);
                decimal nonBillableHours = g.Sum(x => x.NonBillableHours);
                decimal? util = totalHours > 0m ? Math.Round((billableHours / totalHours) * 100m, 2) : null;

                groups.Add(new ProjectTimeGroupDto
                {
                    GroupKey = g.Key.ToString(),
                    GroupName = userName,
                    GroupSubtext = $"{distinctProjects} project{(distinctProjects == 1 ? "" : "s")}",
                    TotalHours = totalHours,
                    TotalBillableHours = billableHours,
                    TotalBilledHours = billedHours,
                    TotalNonBillableHours = nonBillableHours,
                    TotalUtilisationPercent = util,
                    Items = g.OrderBy(x => x.ProjectCode).ThenBy(x => x.TaskName).ToList()
                });
            }
        }

        decimal grandHours = items.Sum(i => i.Hours);
        decimal grandBillable = items.Sum(i => i.BillableHours);
        decimal grandBilled = items.Sum(i => i.BilledHours);
        decimal grandNonBillable = items.Sum(i => i.NonBillableHours);
        decimal? grandUtil = grandHours > 0m ? Math.Round((grandBillable / grandHours) * 100m, 2) : null;

        ProjectTimeSummaryDto summary = new()
        {
            TotalUsers = items.Select(i => i.UserId).Distinct().Count(),
            TotalProjects = items.Select(i => i.ProjectCode).Distinct().Count(),
            TotalHours = grandHours,
            TotalBillableHours = grandBillable,
            TotalBilledHours = grandBilled,
            TotalNonBillableHours = grandNonBillable,
            OverallUtilisationPercent = grandUtil
        };

        return new ProjectTimeByUserReportResponse
        {
            FromDate = request.FromDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ToDate = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            GroupBy = groupByProject ? "project" : "user",
            Groups = groups,
            Summary = summary
        };
    }
}

using Accounting.Entity.Enums;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Api.Services;

/// <summary>
/// Whether a project may take a posting (TK-104): it must be the branch's —
/// the query filter hides every other — and its job must not be over.
/// </summary>
public static class ProjectRules
{
    /// <summary>Active and on-hold projects take postings; completed and cancelled ones do not.</summary>
    public static bool IsPostable(ProjectStatus status) =>
        status is ProjectStatus.Active or ProjectStatus.OnHold;

    /// <summary>Why the projects named may not be posted to, or null when every one may.</summary>
    public static async Task<string?> RefusalAsync(
        AccountingDbContext db, IEnumerable<long?> projectIds, CancellationToken ct)
    {
        List<long> ids = [.. projectIds.Where(id => id is not null).Select(id => id!.Value).Distinct()];
        if (ids.Count == 0)
        {
            return null;
        }

        var found = await db.Projects.AsNoTracking()
            .Where(p => ids.Contains(p.ProjectId))
            .Select(p => new { p.ProjectId, p.ProjectCode, p.Status })
            .ToListAsync(ct);

        foreach (long id in ids)
        {
            var project = found.FirstOrDefault(p => p.ProjectId == id);
            if (project is null)
            {
                return $"Project {id} is not a project of this branch.";
            }

            if (!IsPostable(project.Status))
            {
                return $"Project {project.ProjectCode} is {project.Status.ToString().ToLowerInvariant()}, so nothing more can be posted to it.";
            }
        }

        return null;
    }
}

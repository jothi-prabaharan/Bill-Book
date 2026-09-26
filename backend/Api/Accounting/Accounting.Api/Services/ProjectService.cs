using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Projects;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Services;

/// <summary>
/// The project master (TK-104): a project with its tasks, members and
/// milestones, saved as one form. A project takes its code from the <c>PRJ</c>
/// series when it is created.
///
/// <b>Nothing a posting or a later card leans on is deleted.</b> A task left
/// off the form is deactivated, because time will be logged against it
/// (TK-106); a billed milestone is kept as it is; a project is never deleted,
/// only completed or cancelled, because its ledger rows name it.
/// </summary>
public sealed class ProjectService
{
    private const string SeriesCode = "PRJ";

    private readonly AccountingDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly TimeProvider _clock;
    private readonly ILogger<ProjectService> _log;

    public ProjectService(
        AccountingDbContext db,
        INumberGenerator numbers,
        IBaseCurrencyProvider baseCurrency,
        TimeProvider clock,
        ILogger<ProjectService> log)
    {
        _db = db;
        _numbers = numbers;
        _baseCurrency = baseCurrency;
        _clock = clock;
        _log = log;
    }

    public async Task<List<ProjectListItem>> ListAsync(string? status, long? contactId, CancellationToken ct)
    {
        IQueryable<Project> query = _db.Projects.AsNoTracking();

        if (Enum.TryParse(status, ignoreCase: true, out ProjectStatus wanted))
        {
            query = query.Where(p => p.Status == wanted);
        }

        if (contactId is long client)
        {
            query = query.Where(p => p.ContactId == client);
        }

        List<Project> rows = await query.OrderBy(p => p.ProjectCode).ToListAsync(ct);
        return [.. rows.Select(p => ToListItem(p, new ProjectListItem()))];
    }

    public async Task<ProjectView?> GetAsync(long id, CancellationToken ct)
    {
        Project? project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == id, ct);
        if (project is null)
        {
            return null;
        }

        ProjectView view = ToListItem(project, new ProjectView());
        view.Description = project.Description;
        view.RateBasis = project.RateBasis?.ToString();
        view.HourlyRate = project.HourlyRate;
        view.FixedFee = project.FixedFee;

        view.Tasks = await _db.ProjectTasks.AsNoTracking()
            .Where(t => t.ProjectId == id)
            .OrderBy(t => t.ProjectTaskId)
            .Select(t => new ProjectTaskView
            {
                ProjectTaskId = t.ProjectTaskId,
                TaskName = t.TaskName,
                HourlyRate = t.HourlyRate,
                BudgetHours = t.BudgetHours,
                IsBillable = t.IsBillable,
                IsActive = t.IsActive,
            })
            .ToListAsync(ct);

        view.Members = await _db.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == id)
            .Select(m => new ProjectMemberView { UserId = m.UserId, HourlyRate = m.HourlyRate, CostRate = m.CostRate })
            .ToListAsync(ct);

        view.Milestones = await _db.ProjectMilestones.AsNoTracking()
            .Where(m => m.ProjectId == id)
            .OrderBy(m => m.DueDate).ThenBy(m => m.ProjectMilestoneId)
            .Select(m => new ProjectMilestoneView
            {
                ProjectMilestoneId = m.ProjectMilestoneId,
                Name = m.Name,
                Amount = m.Amount,
                DueDate = m.DueDate,
                InvoiceId = m.InvoiceId,
            })
            .ToListAsync(ct);

        return view;
    }

    /// <summary>The ledger rows tagged with the project, oldest first, in base currency.</summary>
    public async Task<List<ProjectLedgerRow>?> LedgerAsync(long id, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (!await _db.Projects.AnyAsync(p => p.ProjectId == id, ct))
        {
            return null;
        }

        IQueryable<JournalLedger> rows = _db.JournalLedger.AsNoTracking().Where(l => l.ProjectId == id);
        if (from is DateOnly start)
        {
            rows = rows.Where(l => l.LedgerDate >= start);
        }

        if (to is DateOnly end)
        {
            rows = rows.Where(l => l.LedgerDate <= end);
        }

        return await (
            from l in rows
            join a in _db.Accounts on l.AccountId equals a.AccountId
            orderby l.LedgerDate, l.LedgerId
            select new ProjectLedgerRow
            {
                LedgerId = l.LedgerId,
                LedgerDate = l.LedgerDate,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                TransactionTypeCode = l.TransactionTypeCode,
                TransactionId = l.TransactionId,
                DocumentNo = l.DocumentNo,
                Description = l.TransactionDesc,
                Debit = l.DebitAmountBase,
                Credit = l.CreditAmountBase,
            }).ToListAsync(ct);
    }

    /// <summary>The projects among <paramref name="ids"/> in this branch, for another service (<c>internal/projects/exists</c>).</summary>
    public async Task<List<ProjectSummary>> FindAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var rows = await _db.Projects.AsNoTracking()
            .Where(p => ids.Contains(p.ProjectId))
            .Select(p => new { p.ProjectId, p.ProjectCode, p.ProjectName, p.Status })
            .ToListAsync(ct);

        return [.. rows.Select(p => new ProjectSummary
        {
            ProjectId = p.ProjectId,
            ProjectCode = p.ProjectCode,
            ProjectName = p.ProjectName,
            Status = p.Status.ToString(),
            IsPostable = ProjectRules.IsPostable(p.Status),
        })];
    }

    public async Task<SaveProjectResult> SaveAsync(long? id, SaveProjectRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.BillingMethod, ignoreCase: true, out ProjectBillingMethod billing)
            || !Enum.IsDefined(billing)
            || !Enum.TryParse(request.Status, ignoreCase: true, out ProjectStatus status)
            || !Enum.IsDefined(status))
        {
            return new SaveProjectResult(SaveProjectOutcome.InvalidValue, id ?? 0,
                "Choose a billing method and a status from the list.");
        }

        ProjectRateBasis? rateBasis = null;
        if (!string.IsNullOrWhiteSpace(request.RateBasis))
        {
            if (!Enum.TryParse(request.RateBasis, ignoreCase: true, out ProjectRateBasis parsed) || !Enum.IsDefined(parsed))
            {
                return new SaveProjectResult(SaveProjectOutcome.InvalidValue, id ?? 0, "Choose where the hourly rate comes from.");
            }

            rateBasis = parsed;
        }

        if (request.Members.GroupBy(m => m.UserId).Any(g => g.Count() > 1))
        {
            return new SaveProjectResult(SaveProjectOutcome.Refused, id ?? 0, "A user can be a member of a project only once.");
        }

        if (request.StartDate is DateOnly start && request.EndDate is DateOnly end && end < start)
        {
            return new SaveProjectResult(SaveProjectOutcome.Refused, id ?? 0, "The end date is before the start date.");
        }

        Project project;
        if (id is long existingId)
        {
            Project? existing = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectId == existingId, ct);
            if (existing is null)
            {
                return new SaveProjectResult(SaveProjectOutcome.NotFound);
            }

            project = existing;
        }
        else
        {
            string? currency = request.CurrencyCode is { Length: 3 } given
                ? given.ToUpperInvariant()
                : await _baseCurrency.GetBaseCurrencyAsync(ct);
            if (currency is null)
            {
                return new SaveProjectResult(SaveProjectOutcome.Refused, 0,
                    "The branch's base currency could not be read. Nothing was saved.");
            }

            project = new Project { CurrencyCode = currency };
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        if (id is null)
        {
            try
            {
                NumberAllocation code = await _numbers.NextAsync(
                    SeriesCode, DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime), ct);
                project.ProjectCode = code.Code;
            }
            catch (InvalidOperationException ex)
            {
                _log.LogError(ex, "No {Series} numbering series for a new project.", SeriesCode);
                return new SaveProjectResult(SaveProjectOutcome.SeriesMissing, 0,
                    "No project numbering series exists for this branch, so nothing was saved. Re-run the branch seed.");
            }

            _db.Projects.Add(project);
        }
        else if (request.CurrencyCode is { Length: 3 } changed)
        {
            project.CurrencyCode = changed.ToUpperInvariant();
        }

        project.ProjectName = request.ProjectName.Trim();
        project.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        project.ContactId = request.ContactId;
        project.BillingMethod = billing;
        project.RateBasis = billing == ProjectBillingMethod.TimeAndMaterials ? rateBasis ?? ProjectRateBasis.ProjectRate : null;
        project.HourlyRate = request.HourlyRate;
        project.FixedFee = billing == ProjectBillingMethod.FixedFee ? request.FixedFee : null;
        project.BudgetAmount = request.BudgetAmount;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.Status = status;

        await _db.SaveChangesAsync(ct);

        await SaveTasksAsync(project.ProjectId, request.Tasks, ct);
        await SaveMembersAsync(project.ProjectId, request.Members, ct);
        await SaveMilestonesAsync(project.ProjectId, request.Milestones, ct);
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return new SaveProjectResult(SaveProjectOutcome.Ok, project.ProjectId);
    }

    /// <summary>Updates tasks by id, adds new ones, and deactivates the ones left out.</summary>
    private async Task SaveTasksAsync(long projectId, List<SaveProjectTaskRequest> tasks, CancellationToken ct)
    {
        List<ProjectTask> existing = await _db.ProjectTasks.Where(t => t.ProjectId == projectId).ToListAsync(ct);

        foreach (SaveProjectTaskRequest task in tasks)
        {
            ProjectTask? row = task.ProjectTaskId is long taskId ? existing.FirstOrDefault(t => t.ProjectTaskId == taskId) : null;
            if (row is null)
            {
                row = new ProjectTask { ProjectId = projectId };
                _db.ProjectTasks.Add(row);
            }

            row.TaskName = task.TaskName.Trim();
            row.HourlyRate = task.HourlyRate;
            row.BudgetHours = task.BudgetHours;
            row.IsBillable = task.IsBillable;
            row.IsActive = task.IsActive;
        }

        HashSet<long> kept = [.. tasks.Where(t => t.ProjectTaskId is not null).Select(t => t.ProjectTaskId!.Value)];
        foreach (ProjectTask dropped in existing.Where(t => !kept.Contains(t.ProjectTaskId)))
        {
            dropped.IsActive = false;
        }
    }

    /// <summary>Members are keyed by user: updated, added, and removed when left out.</summary>
    private async Task SaveMembersAsync(long projectId, List<SaveProjectMemberRequest> members, CancellationToken ct)
    {
        List<ProjectMember> existing = await _db.ProjectMembers.Where(m => m.ProjectId == projectId).ToListAsync(ct);

        foreach (SaveProjectMemberRequest member in members)
        {
            ProjectMember? row = existing.FirstOrDefault(m => m.UserId == member.UserId);
            if (row is null)
            {
                row = new ProjectMember { ProjectId = projectId, UserId = member.UserId };
                _db.ProjectMembers.Add(row);
            }

            row.HourlyRate = member.HourlyRate;
            row.CostRate = member.CostRate;
        }

        HashSet<Guid> kept = [.. members.Select(m => m.UserId)];
        _db.ProjectMembers.RemoveRange(existing.Where(m => !kept.Contains(m.UserId)));
    }

    /// <summary>Unbilled milestones are updated, added and removed; a billed one stays exactly as it was billed.</summary>
    private async Task SaveMilestonesAsync(long projectId, List<SaveProjectMilestoneRequest> milestones, CancellationToken ct)
    {
        List<ProjectMilestone> existing = await _db.ProjectMilestones.Where(m => m.ProjectId == projectId).ToListAsync(ct);

        foreach (SaveProjectMilestoneRequest milestone in milestones)
        {
            ProjectMilestone? row = milestone.ProjectMilestoneId is long milestoneId
                ? existing.FirstOrDefault(m => m.ProjectMilestoneId == milestoneId)
                : null;

            if (row is { InvoiceId: not null })
            {
                continue;
            }

            if (row is null)
            {
                row = new ProjectMilestone { ProjectId = projectId };
                _db.ProjectMilestones.Add(row);
            }

            row.Name = milestone.Name.Trim();
            row.Amount = milestone.Amount;
            row.DueDate = milestone.DueDate;
        }

        HashSet<long> kept = [.. milestones.Where(m => m.ProjectMilestoneId is not null).Select(m => m.ProjectMilestoneId!.Value)];
        _db.ProjectMilestones.RemoveRange(existing.Where(m => m.InvoiceId is null && !kept.Contains(m.ProjectMilestoneId)));
    }

    private static T ToListItem<T>(Project p, T item) where T : ProjectListItem
    {
        item.ProjectId = p.ProjectId;
        item.ProjectCode = p.ProjectCode;
        item.ProjectName = p.ProjectName;
        item.ContactId = p.ContactId;
        item.BillingMethod = p.BillingMethod.ToString();
        item.Status = p.Status.ToString();
        item.StartDate = p.StartDate;
        item.EndDate = p.EndDate;
        item.BudgetAmount = p.BudgetAmount;
        item.CurrencyCode = p.CurrencyCode;
        return item;
    }
}

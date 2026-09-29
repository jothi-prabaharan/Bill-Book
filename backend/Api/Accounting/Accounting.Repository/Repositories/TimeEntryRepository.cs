using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Repository.Repositories;

/// <summary>
/// The timesheet (TK-106). A user logs their own hours against a project task;
/// someone holding <c>projects.edit</c> may log and correct anyone's. A day
/// holds at most 24 hours across a user's entries. An entry billed on an
/// invoice is fixed until the invoice is voided.
/// </summary>
public sealed class TimeEntryRepository
{
    public const decimal MaxHoursPerDay = 24m;

    private readonly AccountingDbContext _db;

    public TimeEntryRepository(AccountingDbContext db) => _db = db;

    /// <summary>A user's entries between two dates, or everyone's for a reader who may see them.</summary>
    public async Task<List<TimeEntryView>> ListAsync(Guid? userId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        IQueryable<TimeEntry> entries = _db.TimeEntries.AsNoTracking()
            .Where(t => t.WorkDate >= from && t.WorkDate <= to);

        if (userId is Guid user)
        {
            entries = entries.Where(t => t.UserId == user);
        }

        return await (
            from t in entries
            join p in _db.Projects on t.ProjectId equals p.ProjectId
            join k in _db.ProjectTasks on t.ProjectTaskId equals k.ProjectTaskId
            orderby t.WorkDate, p.ProjectCode, k.TaskName
            select new TimeEntryView
            {
                TimeEntryId = t.TimeEntryId,
                ProjectId = t.ProjectId,
                ProjectCode = p.ProjectCode,
                ProjectName = p.ProjectName,
                ProjectTaskId = t.ProjectTaskId,
                TaskName = k.TaskName,
                UserId = t.UserId,
                WorkDate = t.WorkDate,
                Hours = t.Hours,
                Notes = t.Notes,
                IsBillable = t.IsBillable,
                BillingStatus = t.BillingStatus.ToString(),
                InvoiceId = t.InvoiceId,
            }).ToListAsync(ct);
    }

    public async Task<TimeEntryResult> SaveAsync(
        long? id, SaveTimeEntryRequest request, Guid actor, bool mayLogForOthers, CancellationToken ct)
    {
        Guid user = request.UserId ?? actor;
        if (user != actor && !mayLogForOthers)
        {
            return new TimeEntryResult(TimeEntryOutcome.NotYours, id ?? 0, "You can log only your own time.");
        }

        if (request.Hours <= 0m || request.Hours > MaxHoursPerDay)
        {
            return new TimeEntryResult(TimeEntryOutcome.Refused, id ?? 0, "Hours must be more than 0 and at most 24.");
        }

        TimeEntry? entry = null;
        if (id is long existingId)
        {
            entry = await _db.TimeEntries.FirstOrDefaultAsync(t => t.TimeEntryId == existingId, ct);
            if (entry is null)
            {
                return new TimeEntryResult(TimeEntryOutcome.NotFound);
            }

            if (entry.UserId != actor && !mayLogForOthers)
            {
                return new TimeEntryResult(TimeEntryOutcome.NotYours, existingId, "You can change only your own time.");
            }

            if (entry.BillingStatus != TimeBillingStatus.Unbilled)
            {
                return new TimeEntryResult(TimeEntryOutcome.Billed, existingId,
                    "These hours are billed, so they cannot be changed. Void the invoice first.");
            }
        }

        Project? project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId, ct);
        if (project is null || !ProjectRulesRepository.IsPostable(project.Status))
        {
            return new TimeEntryResult(TimeEntryOutcome.Refused, id ?? 0,
                project is null ? "That is not a project of this branch." : $"Project {project.ProjectCode} is {project.Status.ToString().ToLowerInvariant()}, so it takes no more time.");
        }

        ProjectTask? task = await _db.ProjectTasks.AsNoTracking()
            .FirstOrDefaultAsync(k => k.ProjectTaskId == request.ProjectTaskId && k.ProjectId == request.ProjectId, ct);
        if (task is null || !task.IsActive)
        {
            return new TimeEntryResult(TimeEntryOutcome.Refused, id ?? 0, "Choose an active task of that project.");
        }

        decimal otherHours = await _db.TimeEntries
            .Where(t => t.UserId == user && t.WorkDate == request.WorkDate && t.TimeEntryId != (id ?? 0))
            .SumAsync(t => t.Hours, ct);
        if (otherHours + request.Hours > MaxHoursPerDay)
        {
            return new TimeEntryResult(TimeEntryOutcome.Refused, id ?? 0,
                $"That makes {otherHours + request.Hours:0.##} hours on {request.WorkDate:dd MMM yyyy}. A day holds at most 24.");
        }

        if (entry is null)
        {
            entry = new TimeEntry();
            _db.TimeEntries.Add(entry);
        }

        entry.ProjectId = request.ProjectId;
        entry.ProjectTaskId = request.ProjectTaskId;
        entry.UserId = user;
        entry.WorkDate = request.WorkDate;
        entry.Hours = request.Hours;
        entry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entry.IsBillable = request.IsBillable ?? task.IsBillable;

        await _db.SaveChangesAsync(ct);
        return new TimeEntryResult(TimeEntryOutcome.Ok, entry.TimeEntryId);
    }

    public async Task<TimeEntryResult> DeleteAsync(long id, Guid actor, bool mayLogForOthers, CancellationToken ct)
    {
        TimeEntry? entry = await _db.TimeEntries.FirstOrDefaultAsync(t => t.TimeEntryId == id, ct);
        if (entry is null)
        {
            return new TimeEntryResult(TimeEntryOutcome.NotFound);
        }

        if (entry.UserId != actor && !mayLogForOthers)
        {
            return new TimeEntryResult(TimeEntryOutcome.NotYours, id, "You can remove only your own time.");
        }

        if (entry.BillingStatus != TimeBillingStatus.Unbilled)
        {
            return new TimeEntryResult(TimeEntryOutcome.Billed, id, "These hours are billed, so they cannot be removed. Void the invoice first.");
        }

        _db.TimeEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
        return new TimeEntryResult(TimeEntryOutcome.Ok, id);
    }
}

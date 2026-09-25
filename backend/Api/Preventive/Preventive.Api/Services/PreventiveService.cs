using Microsoft.EntityFrameworkCore;
using Preventive.Entity.Enums;
using Preventive.Entity.Models;
using Preventive.Entity.TableEntities;
using Preventive.Repository;
using Shared.Kernel.Employees;
using Shared.Kernel.Persistence;
using Shared.Kernel.School;

namespace Preventive.Api.Services;

public enum PreventiveOutcome
{
    Ok = 1,
    NotFound = 2,
    Invalid = 3,
    StateRule = 4,

    /// <summary>Facility or Employee could not be reached.</summary>
    Unavailable = 5,
}

public sealed record PreventiveResult(PreventiveOutcome Outcome, long? Id = null, string? Detail = null)
{
    public static PreventiveResult Ok(long id) => new(PreventiveOutcome.Ok, id);

    public static PreventiveResult Fail(PreventiveOutcome outcome, string? detail = null) => new(outcome, null, detail);
}

/// <summary>
/// Preventive plans and their occurrences (S7, TK-67). Generation claims each
/// due date by advancing the plan's <c>NextDueDate</c> with a guarded update —
/// the row count is the answer, as in the costing engine — and records the
/// occurrence in the same transaction, unique on plan and due date. Work
/// orders are then raised for scheduled occurrences under the source key
/// <c>PPM:{plan}:{date}</c>, which WorkOrder is idempotent on, so a run that
/// dies between the two steps raises nothing twice when the next one retries.
/// </summary>
public sealed class PreventiveService
{
    /// <summary>At most this many occurrences per plan per run, so a plan dated years back catches up over several runs.</summary>
    public const int CatchUpPerRun = 60;

    private readonly PreventiveDbContext _db;
    private readonly IFacilityClient _facility;
    private readonly IEmployeeDirectory _employees;
    private readonly IWorkOrderClient _workOrders;
    private readonly ILogger<PreventiveService> _log;

    public PreventiveService(
        PreventiveDbContext db,
        IFacilityClient facility,
        IEmployeeDirectory employees,
        IWorkOrderClient workOrders,
        ILogger<PreventiveService> log)
    {
        _db = db;
        _facility = facility;
        _employees = employees;
        _workOrders = workOrders;
        _log = log;
    }

    public async Task<List<PlanView>> PlansAsync(CancellationToken ct) =>
        await _db.PreventivePlans.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PlanView
            {
                PreventivePlanId = p.PreventivePlanId,
                Name = p.Name,
                FacilityAssetId = p.FacilityAssetId,
                SpaceId = p.SpaceId,
                Frequency = p.Frequency,
                Interval = p.Interval,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                NextDueDate = p.NextDueDate,
                LeadDays = p.LeadDays,
                DefaultAssigneeEmployeeId = p.DefaultAssigneeEmployeeId,
                IsActive = p.IsActive,
            })
            .ToListAsync(ct);

    public async Task<List<OccurrenceView>> OccurrencesAsync(long? planId, OccurrenceStatus? status, CancellationToken ct) =>
        await (from o in _db.PreventiveOccurrences.AsNoTracking()
               join p in _db.PreventivePlans.AsNoTracking() on o.PreventivePlanId equals p.PreventivePlanId
               where (planId == null || o.PreventivePlanId == planId) && (status == null || o.OccurrenceStatus == status)
               orderby o.DueDate descending, o.PreventiveOccurrenceId descending
               select new OccurrenceView
               {
                   PreventiveOccurrenceId = o.PreventiveOccurrenceId,
                   PreventivePlanId = o.PreventivePlanId,
                   PlanName = p.Name,
                   DueDate = o.DueDate,
                   WorkOrderId = o.WorkOrderId,
                   WorkOrderNo = o.WorkOrderNo,
                   OccurrenceStatus = o.OccurrenceStatus,
               })
            .Take(1000)
            .ToListAsync(ct);

    public async Task<PreventiveResult> SavePlanAsync(long? id, SavePlanRequest request, CancellationToken ct)
    {
        if (request.EndDate is DateOnly end && end < request.StartDate)
        {
            return PreventiveResult.Fail(PreventiveOutcome.Invalid, "The end date cannot be before the start date.");
        }

        PreventiveResult? checkedRefs = await CheckReferencesAsync(request, ct);
        if (checkedRefs is not null)
        {
            return checkedRefs;
        }

        PreventivePlan? plan;
        if (id is long existing)
        {
            plan = await _db.PreventivePlans.FirstOrDefaultAsync(p => p.PreventivePlanId == existing, ct);
            if (plan is null)
            {
                return PreventiveResult.Fail(PreventiveOutcome.NotFound);
            }
        }
        else
        {
            plan = new PreventivePlan();
            _db.PreventivePlans.Add(plan);
        }

        bool scheduleChanged = id is null
            || plan.StartDate != request.StartDate
            || plan.Frequency != request.Frequency
            || plan.Interval != request.Interval;

        plan.Name = request.Name.Trim();
        plan.FacilityAssetId = request.FacilityAssetId;
        plan.SpaceId = request.SpaceId;
        plan.Frequency = request.Frequency;
        plan.Interval = request.Interval;
        plan.StartDate = request.StartDate;
        plan.EndDate = request.EndDate;
        plan.LeadDays = request.LeadDays;
        plan.DefaultAssigneeEmployeeId = request.DefaultAssigneeEmployeeId;
        plan.IsActive = request.IsActive;

        if (scheduleChanged)
        {
            // A new schedule picks up after the last date already generated, so
            // nothing generated is generated again under the new one.
            DateOnly? last = id is long planId
                ? await _db.PreventiveOccurrences.Where(o => o.PreventivePlanId == planId).MaxAsync(o => (DateOnly?)o.DueDate, ct)
                : null;
            plan.NextDueDate = last is DateOnly after
                ? Recurrence.NextAfter(plan.StartDate, plan.Frequency, plan.Interval, after)
                : plan.StartDate;
        }

        await _db.SaveChangesAsync(ct);
        return PreventiveResult.Ok(plan.PreventivePlanId);
    }

    /// <summary>Skip a scheduled occurrence, or mark a raised one done.</summary>
    public async Task<PreventiveResult> SetOccurrenceAsync(long id, OccurrenceStatus status, CancellationToken ct)
    {
        PreventiveOccurrence? occurrence = await _db.PreventiveOccurrences.FirstOrDefaultAsync(o => o.PreventiveOccurrenceId == id, ct);
        if (occurrence is null)
        {
            return PreventiveResult.Fail(PreventiveOutcome.NotFound);
        }

        bool allowed = (occurrence.OccurrenceStatus, status) switch
        {
            (OccurrenceStatus.Scheduled, OccurrenceStatus.Skipped) => true,
            (OccurrenceStatus.Raised, OccurrenceStatus.Done) => true,
            _ => false,
        };
        if (!allowed)
        {
            return PreventiveResult.Fail(PreventiveOutcome.StateRule,
                "Only an occurrence without a work order can be skipped, and only one with a work order marked done.");
        }

        // Guarded, so the generator raising its work order at the same moment wins or loses cleanly.
        OccurrenceStatus from = occurrence.OccurrenceStatus;
        int changed = await _db.PreventiveOccurrences
            .Where(o => o.PreventiveOccurrenceId == id && o.OccurrenceStatus == from)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OccurrenceStatus, status), ct);
        return changed == 1
            ? PreventiveResult.Ok(id)
            : PreventiveResult.Fail(PreventiveOutcome.StateRule, "The occurrence changed while you were looking at it. Refresh and try again.");
    }

    /// <summary>
    /// Generates the branch's due occurrences and raises their work orders.
    /// Running it twice on one day adds and raises nothing the second time.
    /// </summary>
    public async Task<GenerationResult> GenerateAsync(DateOnly today, CancellationToken ct)
    {
        var result = new GenerationResult();

        List<PreventivePlan> plans = await _db.PreventivePlans.AsNoTracking()
            .Where(p => p.IsActive && (p.EndDate == null || p.NextDueDate <= p.EndDate))
            .ToListAsync(ct);

        foreach (PreventivePlan plan in plans)
        {
            DateOnly due = plan.NextDueDate;
            for (int step = 0; step < CatchUpPerRun && Recurrence.IsInWindow(due, plan.LeadDays, plan.EndDate, today); step++)
            {
                DateOnly next = Recurrence.NextAfter(plan.StartDate, plan.Frequency, plan.Interval, due);
                if (!await ClaimAsync(plan.PreventivePlanId, due, next, ct))
                {
                    break;
                }

                result.Generated++;
                due = next;
            }
        }

        await RaiseScheduledAsync(today, result, ct);
        return result;
    }

    /// <summary>Moves the plan from <paramref name="due"/> to <paramref name="next"/> and records the occurrence, or does neither.</summary>
    private async Task<bool> ClaimAsync(long planId, DateOnly due, DateOnly next, CancellationToken ct)
    {
        await using ITransactionScope transaction = await _db.Database.BeginScopeAsync(ct);

        int claimed = await _db.PreventivePlans
            .Where(p => p.PreventivePlanId == planId && p.NextDueDate == due && p.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.NextDueDate, next), ct);
        if (claimed == 0)
        {
            // Another run, or an edit, moved the plan first.
            return false;
        }

        if (!await _db.PreventiveOccurrences.AnyAsync(o => o.PreventivePlanId == planId && o.DueDate == due, ct))
        {
            _db.PreventiveOccurrences.Add(new PreventiveOccurrence { PreventivePlanId = planId, DueDate = due });
            await _db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return true;
    }

    private async Task RaiseScheduledAsync(DateOnly today, GenerationResult result, CancellationToken ct)
    {
        var pending = await (from o in _db.PreventiveOccurrences
                             join p in _db.PreventivePlans.AsNoTracking() on o.PreventivePlanId equals p.PreventivePlanId
                             where o.OccurrenceStatus == OccurrenceStatus.Scheduled
                             orderby o.DueDate
                             select new { Occurrence = o, Plan = p })
            .Take(500)
            .ToListAsync(ct);

        foreach (var row in pending.Where(r => Recurrence.IsInWindow(r.Occurrence.DueDate, r.Plan.LeadDays, null, today)))
        {
            RaisedWorkOrder raised;
            try
            {
                raised = await _workOrders.RaiseAsync(new RaiseWorkOrder
                {
                    SourceKey = SourceKey(row.Plan.PreventivePlanId, row.Occurrence.DueDate),
                    Title = row.Plan.Name,
                    WorkOrderSource = "Preventive",
                    FacilityAssetId = row.Plan.FacilityAssetId,
                    SpaceId = row.Plan.SpaceId,
                    ReportedDate = today < row.Occurrence.DueDate ? today : row.Occurrence.DueDate,
                    DueDate = row.Occurrence.DueDate,
                    AssignedEmployeeId = row.Plan.DefaultAssigneeEmployeeId,
                    PreventivePlanId = row.Plan.PreventivePlanId,
                }, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _log.LogWarning(ex, "The work order for plan {PlanId} due {DueDate} could not be raised; the next run retries.",
                    row.Plan.PreventivePlanId, row.Occurrence.DueDate);
                result.Failed++;
                continue;
            }

            row.Occurrence.WorkOrderId = raised.WorkOrderId;
            row.Occurrence.WorkOrderNo = raised.WorkOrderNo;
            row.Occurrence.OccurrenceStatus = OccurrenceStatus.Raised;
            await _db.SaveChangesAsync(ct);
            if (raised.Created)
            {
                result.Raised++;
            }
        }
    }

    public static string SourceKey(long planId, DateOnly due) => $"PPM:{planId}:{due:yyyy-MM-dd}";

    private async Task<PreventiveResult?> CheckReferencesAsync(SavePlanRequest request, CancellationToken ct)
    {
        if (request.FacilityAssetId is null && request.SpaceId is null)
        {
            return PreventiveResult.Fail(PreventiveOutcome.Invalid, "Say what the plan maintains: an asset, a space, or both.");
        }

        try
        {
            FacilityLookupResponse found = await _facility.LookupAsync(
                request.FacilityAssetId is long a ? [a] : [], request.SpaceId is long s ? [s] : [], ct);
            bool assetOk = request.FacilityAssetId is null || found.Assets.Any(x => x.Id == request.FacilityAssetId && x.IsUsable);
            bool spaceOk = request.SpaceId is null || found.Spaces.Any(x => x.Id == request.SpaceId && x.IsUsable);
            if (!assetOk || !spaceOk)
            {
                return PreventiveResult.Fail(PreventiveOutcome.Invalid, "Choose an asset in use and an active space of this branch.");
            }

            if (request.DefaultAssigneeEmployeeId is long employeeId)
            {
                IReadOnlyDictionary<long, EmployeeProfile> employees = await _employees.FindAsync([employeeId], ct);
                if (!employees.TryGetValue(employeeId, out EmployeeProfile? employee) || employee.EmployeeStatus == "Exited")
                {
                    return PreventiveResult.Fail(PreventiveOutcome.Invalid, "Choose a current employee of this branch.");
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "A preventive plan's asset, space or assignee could not be checked.");
            return PreventiveResult.Fail(PreventiveOutcome.Unavailable);
        }

        return null;
    }
}

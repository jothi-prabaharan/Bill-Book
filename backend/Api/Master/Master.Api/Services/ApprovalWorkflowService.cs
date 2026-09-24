using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Approvals;
using Shared.Kernel.Apps;

namespace Master.Api.Services;

/// <summary>
/// The approval workflows a branch configures (D-26, TK-99), and the defaults
/// a branch starts with. Editing a workflow replaces its levels; requests
/// already in flight keep the steps they were given at submission.
/// </summary>
public sealed class ApprovalWorkflowService
{
    private readonly ContactsDbContext _db;

    public ApprovalWorkflowService(ContactsDbContext db) => _db = db;

    public async Task<IReadOnlyList<ApprovalWorkflowView>> ListAsync(CancellationToken ct)
    {
        List<ApprovalWorkflow> rows = await _db.ApprovalWorkflows.AsNoTracking()
            .Include(w => w.Levels)
            .OrderBy(w => w.RequestKind).ThenBy(w => w.Name)
            .ToListAsync(ct);

        return rows.Select(w => new ApprovalWorkflowView
        {
            ApprovalWorkflowId = w.ApprovalWorkflowId,
            App = w.App.ToString(),
            Name = w.Name,
            RequestKind = w.RequestKind,
            DepartmentId = w.DepartmentId,
            GradeId = w.GradeId,
            WorkLocationId = w.WorkLocationId,
            EffectiveFrom = w.EffectiveFrom,
            IsActive = w.IsActive,
            Levels = w.Levels.OrderBy(l => l.Sequence).Select(l => new SaveApprovalLevel
            {
                Label = l.Label, ApproverKind = l.ApproverKind, ReportingDepth = l.ReportingDepth,
                RelationshipTypeId = l.RelationshipTypeId, RoleId = l.RoleId, EmployeeId = l.EmployeeId, UserId = l.UserId,
                AboveAmount = l.AboveAmount, IsOptional = l.IsOptional, CanEdit = l.CanEdit,
                IsCommentRequired = l.IsCommentRequired, EscalateAfterDays = l.EscalateAfterDays,
            }).ToList(),
        }).ToList();
    }

    public async Task<ApprovalWorkflowResult> SaveAsync(long? id, SaveApprovalWorkflowRequest request, CancellationToken ct)
    {
        if (IncompleteLevel(request.Levels) is string problem)
        {
            return new ApprovalWorkflowResult(ApprovalWorkflowOutcome.IncompleteLevel, Detail: problem);
        }

        ApprovalWorkflow? workflow = id is long existing
            ? await _db.ApprovalWorkflows.Include(w => w.Levels).FirstOrDefaultAsync(w => w.ApprovalWorkflowId == existing, ct)
            : new ApprovalWorkflow();
        if (workflow is null)
        {
            return new ApprovalWorkflowResult(ApprovalWorkflowOutcome.NotFound);
        }

        workflow.Name = request.Name.Trim();
        workflow.RequestKind = request.RequestKind;
        workflow.App = AppOf(request.RequestKind);
        workflow.DepartmentId = request.DepartmentId;
        workflow.GradeId = request.GradeId;
        workflow.WorkLocationId = request.WorkLocationId;
        workflow.EffectiveFrom = request.EffectiveFrom;
        workflow.IsActive = request.IsActive;

        if (id is null)
        {
            _db.ApprovalWorkflows.Add(workflow);
        }
        else
        {
            _db.ApprovalWorkflowLevels.RemoveRange(workflow.Levels);
            await _db.SaveChangesAsync(ct);
            workflow.Levels.Clear();
        }

        int sequence = 1;
        foreach (SaveApprovalLevel level in request.Levels)
        {
            workflow.Levels.Add(new ApprovalWorkflowLevel
            {
                Sequence = sequence++,
                Label = level.Label.Trim(),
                ApproverKind = level.ApproverKind,
                ReportingDepth = level.ApproverKind == ApproverKind.ReportingChain ? level.ReportingDepth ?? 1 : null,
                RelationshipTypeId = level.ApproverKind == ApproverKind.Relationship ? level.RelationshipTypeId : null,
                RoleId = level.ApproverKind == ApproverKind.RoleHolder ? level.RoleId : null,
                EmployeeId = level.ApproverKind == ApproverKind.NamedEmployee ? level.EmployeeId : null,
                UserId = level.ApproverKind == ApproverKind.NamedUser ? level.UserId : null,
                AboveAmount = level.AboveAmount,
                IsOptional = level.IsOptional,
                CanEdit = level.CanEdit,
                IsCommentRequired = level.IsCommentRequired,
                EscalateAfterDays = level.EscalateAfterDays,
            });
        }

        await _db.SaveChangesAsync(ct);
        return new ApprovalWorkflowResult(ApprovalWorkflowOutcome.Ok, workflow.ApprovalWorkflowId);
    }

    /// <summary>
    /// A branch's starting workflows for the apps it holds (TK-49): for HRMS,
    /// leave goes to the employee's manager. The level is optional, so the one
    /// person with no manager has their leave approved on submission rather than
    /// never. Idempotent: only a kind with no workflow at all gets one.
    /// </summary>
    public async Task<int> SeedDefaultsAsync(Guid orgId, App apps, DateOnly effectiveFrom, CancellationToken ct)
    {
        if (!apps.HasFlag(App.Hrms))
        {
            return 0;
        }

        int added = 0;
        foreach (ApprovalRequestKind kind in new[] { ApprovalRequestKind.Leave, ApprovalRequestKind.LeaveEncashment })
        {
            if (await _db.ApprovalWorkflows.IgnoreQueryFilters().AnyAsync(w => w.OrgId == orgId && w.RequestKind == kind, ct))
            {
                continue;
            }

            _db.ApprovalWorkflows.Add(new ApprovalWorkflow
            {
                OrgId = orgId,
                Name = kind == ApprovalRequestKind.Leave ? "Default leave" : "Default leave encashment",
                RequestKind = kind,
                App = App.Hrms,
                EffectiveFrom = effectiveFrom,
                Levels =
                [
                    new ApprovalWorkflowLevel
                    {
                        OrgId = orgId,
                        Sequence = 1,
                        Label = "Manager",
                        ApproverKind = ApproverKind.ReportingChain,
                        ReportingDepth = 1,
                        IsOptional = true,
                    },
                ],
            });
            added++;
        }

        await _db.SaveChangesAsync(ct);
        return added;
    }

    /// <summary>The app a request kind belongs to: HRMS's below 100, RetailErp's from 101.</summary>
    public static App AppOf(ApprovalRequestKind kind) =>
        (int)kind > 100 ? App.RetailErp
        : kind is ApprovalRequestKind.SalaryRevision or ApprovalRequestKind.Loan or ApprovalRequestKind.FullAndFinal ? App.Payroll
        : App.Hrms;

    /// <summary>The first level missing what its kind needs, as a sentence, or null. Public for tests.</summary>
    public static string? IncompleteLevel(IReadOnlyList<SaveApprovalLevel> levels)
    {
        foreach (SaveApprovalLevel level in levels)
        {
            bool complete = level.ApproverKind switch
            {
                ApproverKind.Relationship => level.RelationshipTypeId is not null,
                ApproverKind.RoleHolder => level.RoleId is not null,
                ApproverKind.NamedEmployee => level.EmployeeId is not null,
                ApproverKind.NamedUser => level.UserId is not null,
                _ => true,
            };

            if (!complete)
            {
                return $"Choose who approves at the {level.Label} level.";
            }
        }

        return null;
    }
}

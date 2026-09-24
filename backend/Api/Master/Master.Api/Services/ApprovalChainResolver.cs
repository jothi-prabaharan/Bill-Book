using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Approvals;

namespace Master.Api.Services;

/// <summary>
/// Resolves an approval chain for one request (D-26, TK-99): finds the
/// workflow, keeps the levels the amount calls for, finds each level's
/// approver, and applies the skip rules. The owning service stores the answer
/// as its steps, so the chain is a snapshot from here on.
///
/// <list type="bullet">
/// <item><b>The most specific workflow wins</b>, among the active ones in force
/// on the submission date: one naming the requester's department, grade and
/// location beats one naming fewer, and the latest <c>EffectiveFrom</c> breaks a tie.</item>
/// <item><b>A level with <c>AboveAmount</c> applies only above it.</b></item>
/// <item><b>Users and roles are Master's; employees are Hrm's</b>, asked in one call.</item>
/// <item><b>Skip rules:</b> a level whose approver is the requester, or the same
/// person as the level before, is skipped so nobody approves twice or approves
/// their own request; an optional level with no approver is skipped; a required
/// level with no approver refuses the whole chain and says which level.</item>
/// </list>
/// </summary>
public sealed class ApprovalChainResolver
{
    private readonly ContactsDbContext _db;
    private readonly IHrmApproverClient _hrm;
    private readonly ILogger<ApprovalChainResolver> _log;

    public ApprovalChainResolver(ContactsDbContext db, IHrmApproverClient hrm, ILogger<ApprovalChainResolver> log)
    {
        _db = db;
        _hrm = hrm;
        _log = log;
    }

    public async Task<ResolveChainResponse> ResolveAsync(ResolveChainRequest request, CancellationToken ct)
    {
        List<ApprovalWorkflow> candidates = await _db.ApprovalWorkflows
            .AsNoTracking()
            .Include(w => w.Levels)
            .Where(w => w.IsActive && w.RequestKind == request.RequestKind && w.EffectiveFrom <= request.OnDate)
            .ToListAsync(ct);

        ApprovalWorkflow? workflow = MostSpecific(candidates, request);
        if (workflow is null)
        {
            return new ResolveChainResponse { Outcome = ResolveChainOutcome.NoWorkflow };
        }

        List<ApprovalWorkflowLevel> levels = workflow.Levels
            .Where(l => AppliesTo(l, request.Amount))
            .OrderBy(l => l.Sequence)
            .ToList();

        Dictionary<int, EmployeeApproverAnswer> employees;
        try
        {
            employees = await EmployeeApproversAsync(levels, request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            _log.LogError(ex, "Employee approvers for {Kind} could not be resolved.", request.RequestKind);
            return new ResolveChainResponse
            {
                Outcome = ResolveChainOutcome.Unresolvable,
                WorkflowName = workflow.Name,
                Detail = "The approvers could not be looked up just now. Try again shortly.",
            };
        }

        return Apply(workflow.Name, levels, employees, request.RequesterUserId);
    }

    /// <summary>The workflow that names the most of the requester's department, grade and location. Public for tests.</summary>
    public static ApprovalWorkflow? MostSpecific(IEnumerable<ApprovalWorkflow> candidates, ResolveChainRequest request) =>
        candidates
            .Where(w => (w.DepartmentId is null || w.DepartmentId == request.DepartmentId)
                && (w.GradeId is null || w.GradeId == request.GradeId)
                && (w.WorkLocationId is null || w.WorkLocationId == request.WorkLocationId))
            .OrderByDescending(w => (w.DepartmentId is null ? 0 : 1) + (w.GradeId is null ? 0 : 1) + (w.WorkLocationId is null ? 0 : 1))
            .ThenByDescending(w => w.EffectiveFrom)
            .ThenByDescending(w => w.ApprovalWorkflowId)
            .FirstOrDefault();

    /// <summary>Whether a level applies to a request of this amount. Public for tests.</summary>
    public static bool AppliesTo(ApprovalWorkflowLevel level, decimal? amount) =>
        level.AboveAmount is not decimal floor || (amount is decimal value && value > floor);

    /// <summary>
    /// Turns resolved levels into steps under the skip rules. Pure, and public,
    /// so every rule is tested without a database or Hrm.
    /// </summary>
    public static ResolveChainResponse Apply(
        string workflowName,
        IReadOnlyList<ApprovalWorkflowLevel> levels,
        IReadOnlyDictionary<int, EmployeeApproverAnswer> employees,
        Guid? requester)
    {
        var steps = new List<ResolvedStep>();
        Guid? previousApprover = null;

        foreach (ApprovalWorkflowLevel level in levels)
        {
            Guid? user = null;
            long? employee = null;
            int? role = null;

            switch (level.ApproverKind)
            {
                case ApproverKind.RoleHolder:
                    role = level.RoleId;
                    break;
                case ApproverKind.NamedUser:
                    user = level.UserId;
                    break;
                default:
                    if (employees.TryGetValue(level.Sequence, out EmployeeApproverAnswer? answer) && answer.UserId is not null)
                    {
                        user = answer.UserId;
                        employee = answer.EmployeeId;
                    }

                    break;
            }

            bool resolved = user is not null || role is not null;
            var step = new ResolvedStep
            {
                Sequence = level.Sequence,
                Label = level.Label,
                ApproverUserId = user,
                ApproverEmployeeId = employee,
                RoleId = role,
                IsCommentRequired = level.IsCommentRequired,
                CanEdit = level.CanEdit,
                EscalateAfterDays = level.EscalateAfterDays,
            };

            if (!resolved)
            {
                if (!level.IsOptional)
                {
                    return new ResolveChainResponse
                    {
                        Outcome = ResolveChainOutcome.Unresolvable,
                        WorkflowName = workflowName,
                        Detail = $"The {level.Label} level has no one to approve this request. Ask your administrator to set its approver.",
                    };
                }

                step.IsSkipped = true;
            }
            else if (user is not null && (user == requester || user == previousApprover))
            {
                // Nobody approves their own request, and nobody approves twice.
                step.IsSkipped = true;
            }
            else
            {
                previousApprover = user ?? previousApprover;
            }

            steps.Add(step);
        }

        return new ResolveChainResponse { Outcome = ResolveChainOutcome.Resolved, WorkflowName = workflowName, Steps = steps };
    }

    /// <summary>Whether <paramref name="actor"/> is an active delegate of <paramref name="approver"/> on <paramref name="on"/>.</summary>
    public Task<bool> IsDelegateAsync(Guid approver, Guid actor, DateOnly on, CancellationToken ct) =>
        _db.ApprovalDelegates.AnyAsync(
            d => d.UserId == approver && d.DelegateUserId == actor && d.FromDate <= on && d.ToDate >= on, ct);

    private async Task<Dictionary<int, EmployeeApproverAnswer>> EmployeeApproversAsync(
        IReadOnlyList<ApprovalWorkflowLevel> levels, ResolveChainRequest request, CancellationToken ct)
    {
        List<EmployeeApproverQuery> queries = levels
            .Where(l => l.ApproverKind is ApproverKind.ReportingChain or ApproverKind.Relationship
                or ApproverKind.DepartmentHead or ApproverKind.NamedEmployee)
            .Select(l => new EmployeeApproverQuery
            {
                Sequence = l.Sequence,
                Kind = l.ApproverKind,
                ReportingDepth = l.ReportingDepth,
                RelationshipTypeId = l.RelationshipTypeId,
                NamedEmployeeId = l.EmployeeId,
            })
            .ToList();

        // No employee levels, or a requester who is not an employee (a RetailErp
        // user): nothing to ask, and those levels are simply unresolved.
        if (queries.Count == 0 || request.RequesterEmployeeId is not long employeeId)
        {
            return [];
        }

        IReadOnlyList<EmployeeApproverAnswer> answers = await _hrm.ResolveAsync(new ResolveEmployeesRequest
        {
            CustomerId = request.CustomerId,
            OrgId = request.OrgId,
            EmployeeId = employeeId,
            Levels = queries,
        }, ct);

        return answers.ToDictionary(a => a.Sequence);
    }
}

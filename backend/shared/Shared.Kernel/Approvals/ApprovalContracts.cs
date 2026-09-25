using System.ComponentModel.DataAnnotations;

namespace Shared.Kernel.Approvals;

/// <summary>
/// Asks Master to resolve a chain (D-26, TK-99): <c>POST internal/approval-chains/resolve</c>.
/// The owning service sends what the request is, who raised it and for how much;
/// Master finds the workflow, resolves each level and applies the skip rules.
/// </summary>
public sealed class ResolveChainRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public ApprovalRequestKind RequestKind { get; set; }

    /// <summary>The requester's login, so they never approve their own request.</summary>
    public Guid? RequesterUserId { get; set; }

    /// <summary>The employee the request is for, for the employee-based approver kinds.</summary>
    public long? RequesterEmployeeId { get; set; }

    public long? DepartmentId { get; set; }

    public long? GradeId { get; set; }

    public long? WorkLocationId { get; set; }

    /// <summary>The amount a level's <c>AboveAmount</c> is compared with, in base currency.</summary>
    public decimal? Amount { get; set; }

    /// <summary>The submission date: the workflow in force on it applies.</summary>
    public DateOnly OnDate { get; set; }
}

public enum ResolveChainOutcome
{
    /// <summary>A workflow matched and every required level resolved.</summary>
    Resolved = 1,

    /// <summary>No workflow for the kind: the request's ordinary approve action applies.</summary>
    NoWorkflow = 2,

    /// <summary>A required level has no approver; <see cref="ResolveChainResponse.Detail"/> names it.</summary>
    Unresolvable = 3,
}

public sealed class ResolveChainResponse
{
    public ResolveChainOutcome Outcome { get; set; }

    public string? WorkflowName { get; set; }

    /// <summary>For an unresolvable chain: which level, and why, in a sentence.</summary>
    public string? Detail { get; set; }

    public List<ResolvedStep> Steps { get; set; } = [];
}

/// <summary>One level of a resolved chain; the owning service stores it as a step.</summary>
public sealed class ResolvedStep
{
    public int Sequence { get; set; }

    public string Label { get; set; } = null!;

    public Guid? ApproverUserId { get; set; }

    public long? ApproverEmployeeId { get; set; }

    public int? RoleId { get; set; }

    /// <summary>Skipped by a skip rule (requester, repeat approver, optional and unresolvable).</summary>
    public bool IsSkipped { get; set; }

    public bool IsCommentRequired { get; set; }

    public bool CanEdit { get; set; }

    public int? EscalateAfterDays { get; set; }
}

/// <summary>
/// Asks Employee for the approvers only it can find (D-26, TK-49):
/// <c>POST internal/approval-chains/resolve-employees</c>.
/// </summary>
public sealed class ResolveEmployeesRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long EmployeeId { get; set; }

    public List<EmployeeApproverQuery> Levels { get; set; } = [];
}

public sealed class EmployeeApproverQuery
{
    public int Sequence { get; set; }

    public ApproverKind Kind { get; set; }

    [Range(1, 10, ErrorMessage = "Reporting depth must be between 1 and 10.")]
    public int? ReportingDepth { get; set; }

    public long? RelationshipTypeId { get; set; }

    public long? NamedEmployeeId { get; set; }
}

/// <summary>One level's approver as Employee found them, or none.</summary>
public sealed class EmployeeApproverAnswer
{
    public int Sequence { get; set; }

    public long? EmployeeId { get; set; }

    /// <summary>The approver's login. An approver with none cannot act, so the level is unresolved.</summary>
    public Guid? UserId { get; set; }
}

/// <summary>Is <see cref="ActorUserId"/> an active delegate of <see cref="ApproverUserId"/> on <see cref="OnDate"/>?</summary>
public sealed class DelegateCheckRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public Guid ApproverUserId { get; set; }

    public Guid ActorUserId { get; set; }

    public DateOnly OnDate { get; set; }
}

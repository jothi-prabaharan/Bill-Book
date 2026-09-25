using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Approvals;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;

namespace Master.Entity.TableEntities;

/// <summary>
/// One approval chain for one kind of request (D-26, TK-99), in the tenant
/// schema <c>apr</c>. The branch configures how many levels, what each is
/// called and who stands at each; nothing about levels is fixed in code.
///
/// <b>The most specific match wins</b>: a workflow for the requester's
/// department, grade and location beats one for the department alone, which
/// beats the fallback with all three null. A workflow applies to requests
/// submitted on or after <see cref="EffectiveFrom"/>; a request already in flight
/// keeps the steps it was given.
/// </summary>
public class ApprovalWorkflow : OrgScopedEntity
{
    public long ApprovalWorkflowId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public ApprovalRequestKind RequestKind { get; set; }

    /// <summary>The app whose requests this governs, for the settings screen's filter.</summary>
    public App App { get; set; } = App.RetailErp;

    /// <summary>HRMS matching. Ignored for RetailErp kinds.</summary>
    public long? DepartmentId { get; set; }

    public long? GradeId { get; set; }

    public long? WorkLocationId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ApprovalWorkflowLevel> Levels { get; set; } = [];
}

/// <summary>One level of a workflow, in order.</summary>
public class ApprovalWorkflowLevel : OrgScopedEntity
{
    public long ApprovalWorkflowLevelId { get; set; }

    public long ApprovalWorkflowId { get; set; }

    [Range(1, 50, ErrorMessage = "Sequence must be between 1 and 50.")]
    public int Sequence { get; set; }

    [Required(ErrorMessage = "Label is required.")]
    [MaxLength(50, ErrorMessage = "Label cannot exceed 50 characters.")]
    public string Label { get; set; } = null!;

    public ApproverKind ApproverKind { get; set; }

    /// <summary>For ReportingChain: 1 is the direct manager, 2 their manager.</summary>
    [Range(1, 10, ErrorMessage = "Reporting depth must be between 1 and 10.")]
    public int? ReportingDepth { get; set; }

    /// <summary>For Relationship: Employee's relationship type ("Lead").</summary>
    public long? RelationshipTypeId { get; set; }

    /// <summary>For RoleHolder: any holder of this role in the branch.</summary>
    public int? RoleId { get; set; }

    /// <summary>For NamedEmployee.</summary>
    public long? EmployeeId { get; set; }

    /// <summary>For NamedUser.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The level applies only when the request's amount exceeds this.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The amount cannot be negative.")]
    public decimal? AboveAmount { get; set; }

    /// <summary>Skipped, not blocked, when no approver can be found.</summary>
    public bool IsOptional { get; set; }

    public bool CanEdit { get; set; }

    public bool IsCommentRequired { get; set; }

    [Range(1, 365, ErrorMessage = "Escalation must be between 1 and 365 days.")]
    public int? EscalateAfterDays { get; set; }
}

/// <summary>While active, <see cref="DelegateUserId"/> acts in <see cref="UserId"/>'s place, and the step records both.</summary>
public class ApprovalDelegate : OrgScopedEntity
{
    public long ApprovalDelegateId { get; set; }

    public Guid UserId { get; set; }

    public Guid DelegateUserId { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }
}

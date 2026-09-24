using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Approvals;

/// <summary>
/// One level of a resolved chain, stored by the service that owns the request
/// (D-26, TK-99): <c>tla.LeaveApprovalSteps</c>, <c>pur.ApprovalSteps</c> and so
/// on, each a subclass of this. Snapshotted at submission, so a later change of
/// manager or workflow never moves a request already in flight.
///
/// <b>The approver is a user, an employee, or a role.</b> A RetailErp approver
/// is a user; an HRMS approver is an employee who has a user; a role step is
/// taken by whichever holder acts first.
/// </summary>
public abstract class ApprovalStepBase : OrgScopedEntity
{
    public long RequestId { get; set; }

    public ApprovalRequestKind RequestKind { get; set; }

    public int Sequence { get; set; }

    /// <summary>What the customer calls the level: "Manager", "HR". Copied at resolution.</summary>
    [Required(ErrorMessage = "Label is required.")]
    [MaxLength(50, ErrorMessage = "Label cannot exceed 50 characters.")]
    public string Label { get; set; } = null!;

    public Guid? ApproverUserId { get; set; }

    public long? ApproverEmployeeId { get; set; }

    /// <summary>For a role step: any holder of this role in the branch may act.</summary>
    public int? RoleId { get; set; }

    public ApprovalStepStatus StepStatus { get; set; } = ApprovalStepStatus.Waiting;

    public Guid? ActedByUserId { get; set; }

    public DateTimeOffset? ActedAt { get; set; }

    [MaxLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    public string? Comments { get; set; }

    public DateOnly? DueDate { get; set; }

    public bool IsCommentRequired { get; set; }
}

using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Approvals;

namespace Master.Entity.Models;

/// <summary>Settings › Approval workflows: one chain and its levels (D-26, TK-99).</summary>
public class SaveApprovalWorkflowRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public ApprovalRequestKind RequestKind { get; set; }

    public long? DepartmentId { get; set; }

    public long? GradeId { get; set; }

    public long? WorkLocationId { get; set; }

    [Required(ErrorMessage = "Effective date is required.")]
    public DateOnly EffectiveFrom { get; set; }

    public bool IsActive { get; set; } = true;

    [MinLength(1, ErrorMessage = "Add at least one level.")]
    public List<SaveApprovalLevel> Levels { get; set; } = [];
}

public class SaveApprovalLevel
{
    [Required(ErrorMessage = "Label is required.")]
    [MaxLength(50, ErrorMessage = "Label cannot exceed 50 characters.")]
    public string Label { get; set; } = null!;

    public ApproverKind ApproverKind { get; set; }

    [Range(1, 10, ErrorMessage = "Reporting depth must be between 1 and 10.")]
    public int? ReportingDepth { get; set; }

    public long? RelationshipTypeId { get; set; }

    public int? RoleId { get; set; }

    public long? EmployeeId { get; set; }

    public Guid? UserId { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The amount cannot be negative.")]
    public decimal? AboveAmount { get; set; }

    public bool IsOptional { get; set; }

    public bool CanEdit { get; set; }

    public bool IsCommentRequired { get; set; }

    [Range(1, 365, ErrorMessage = "Escalation must be between 1 and 365 days.")]
    public int? EscalateAfterDays { get; set; }
}

public class ApprovalWorkflowView : SaveApprovalWorkflowRequest
{
    public long ApprovalWorkflowId { get; set; }

    public string App { get; set; } = null!;
}

public enum ApprovalWorkflowOutcome
{
    Ok = 1,
    NotFound = 2,

    /// <summary>A level is missing what its approver kind needs (a depth, a role, a person).</summary>
    IncompleteLevel = 3,
}

public sealed record ApprovalWorkflowResult(ApprovalWorkflowOutcome Outcome, long Id = 0, string? Detail = null);

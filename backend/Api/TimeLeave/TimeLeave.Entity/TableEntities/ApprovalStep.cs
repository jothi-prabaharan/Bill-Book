using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;
using TimeLeave.Entity.Enums;

namespace TimeLeave.Entity.TableEntities;

public class ApprovalStep : OrgScopedEntity
{
    [Key]
    public long ApprovalStepId { get; set; }

    public RequestKind RequestKind { get; set; }

    public long RequestId { get; set; }

    public int Sequence { get; set; }

    [Required(ErrorMessage = "Step label is required.")]
    [MaxLength(50, ErrorMessage = "Step label cannot exceed 50 characters.")]
    public string Label { get; set; } = string.Empty;

    public long? ApproverEmployeeId { get; set; }

    public int? RoleId { get; set; }

    public ApprovalStepStatus StepStatus { get; set; } = ApprovalStepStatus.Waiting;

    public Guid? ActedByUserId { get; set; }

    public DateTimeOffset? ActedAt { get; set; }

    [MaxLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    public string? Comments { get; set; }

    public DateOnly? DueDate { get; set; }
}

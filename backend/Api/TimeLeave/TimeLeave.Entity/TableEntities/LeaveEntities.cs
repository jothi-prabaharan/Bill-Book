using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;
using TimeLeave.Entity.Enums;

namespace TimeLeave.Entity.TableEntities;

public class LeaveType : OrgScopedEntity
{
    [Key]
    public long LeaveTypeId { get; set; }

    [Required(ErrorMessage = "Leave type code is required.")]
    [MaxLength(10, ErrorMessage = "Code cannot exceed 10 characters.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Leave type name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public bool IsPaid { get; set; } = true;

    public bool IsHalfDayAllowed { get; set; } = true;

    [Column(TypeName = "decimal(18,4)")]
    public decimal? IsAttachmentRequiredAboveDays { get; set; }

    public Gender? Gender { get; set; }

    public bool IsActive { get; set; } = true;
}

public class LeavePolicy : OrgScopedEntity
{
    [Key]
    public long LeavePolicyId { get; set; }

    [Required(ErrorMessage = "Leave type is required.")]
    public long LeaveTypeId { get; set; }

    public LeaveType? LeaveType { get; set; }

    public long? GradeId { get; set; }

    public long? WorkLocationId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal AnnualQuota { get; set; }

    public AccrualKind AccrualKind { get; set; } = AccrualKind.Monthly;

    public bool IsProratedOnJoining { get; set; } = true;

    public CarryForwardKind CarryForwardKind { get; set; } = CarryForwardKind.Lapse;

    [Column(TypeName = "decimal(18,4)")]
    public decimal? MaxCarryForward { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? MaxEncashPerYear { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? MinDaysPerApplication { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? MaxDaysPerApplication { get; set; }

    public int NoticeDays { get; set; }

    public bool IsSandwichRule { get; set; }

    public bool CanApplyInProbation { get; set; } = true;
}

public class LeaveBalance : OrgScopedEntity
{
    [Key]
    public long LeaveBalanceId { get; set; }

    public long EmployeeId { get; set; }

    public long LeaveTypeId { get; set; }

    public LeaveType? LeaveType { get; set; }

    public int LeaveYear { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Opening { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Accrued { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Taken { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Encashed { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Lapsed { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Adjusted { get; set; }
}

public class LeaveApplication : OrgScopedEntity
{
    [Key]
    public long LeaveApplicationId { get; set; }

    public long EmployeeId { get; set; }

    public long LeaveTypeId { get; set; }

    public LeaveType? LeaveType { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public LeaveHalf FromHalf { get; set; } = LeaveHalf.Full;

    public LeaveHalf ToHalf { get; set; } = LeaveHalf.Full;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Days { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Attachment key cannot exceed 500 characters.")]
    public string? AttachmentKey { get; set; }

    public LeaveStatus LeaveStatus { get; set; } = LeaveStatus.Draft;

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    [MaxLength(50, ErrorMessage = "Step label cannot exceed 50 characters.")]
    public string? CurrentStepLabel { get; set; }

    public long? CurrentApproverEmployeeId { get; set; }
}

public class LeaveEncashment : OrgScopedEntity
{
    [Key]
    public long LeaveEncashmentId { get; set; }

    public long EmployeeId { get; set; }

    public long LeaveTypeId { get; set; }

    public LeaveType? LeaveType { get; set; }

    public int LeaveYear { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Days { get; set; }

    public long? PayrollRunId { get; set; }

    public EncashmentStatus EncashmentStatus { get; set; } = EncashmentStatus.Draft;

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    [MaxLength(50, ErrorMessage = "Step label cannot exceed 50 characters.")]
    public string? CurrentStepLabel { get; set; }

    public long? CurrentApproverEmployeeId { get; set; }
}

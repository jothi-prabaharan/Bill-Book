using System.ComponentModel.DataAnnotations;
using Claims.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Claims.Entity.TableEntities;

public class ClaimCategory : OrgScopedEntity
{
    public long ClaimCategoryId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public bool IsReceiptRequired { get; set; }

    public long? LedgerAccountId { get; set; }

    public bool IsTaxable { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ClaimLimit> Limits { get; set; } = new List<ClaimLimit>();
}

public class ClaimLimit : OrgScopedEntity
{
    public long ClaimLimitId { get; set; }

    public long ClaimCategoryId { get; set; }

    public long? GradeId { get; set; }

    public LimitPeriod LimitPeriod { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Amount must be positive.")]
    public decimal Amount { get; set; }

    public ClaimCategory Category { get; set; } = null!;
}

public class ExpenseClaim : OrgScopedEntity
{
    public long ExpenseClaimId { get; set; }

    [Required(ErrorMessage = "Claim number is required.")]
    [MaxLength(30, ErrorMessage = "Claim number cannot exceed 30 characters.")]
    public string ClaimNo { get; set; } = null!;

    public long EmployeeId { get; set; }

    public DateOnly ClaimDate { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal ApprovedAmount { get; set; }

    public ClaimStatus ClaimStatus { get; set; } = ClaimStatus.Draft;

    public PayoutMode PayoutMode { get; set; } = PayoutMode.Direct;

    public long? PayrollRunId { get; set; }

    public long? SpendMoneyId { get; set; }

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    [MaxLength(50, ErrorMessage = "Step label cannot exceed 50 characters.")]
    public string? CurrentStepLabel { get; set; }

    public long? CurrentApproverEmployeeId { get; set; }

    public ICollection<ExpenseClaimLine> Lines { get; set; } = new List<ExpenseClaimLine>();

    public ICollection<ApprovalStep> Steps { get; set; } = new List<ApprovalStep>();
}

public class ExpenseClaimLine : OrgScopedEntity
{
    public long ExpenseClaimLineId { get; set; }

    public long ExpenseClaimId { get; set; }

    public long ClaimCategoryId { get; set; }

    public DateOnly ExpenseDate { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string Description { get; set; } = null!;

    [Range(0, 1000000000, ErrorMessage = "Amount must be positive.")]
    public decimal Amount { get; set; }

    [MaxLength(500, ErrorMessage = "Receipt attachment key cannot exceed 500 characters.")]
    public string? ReceiptAttachmentKey { get; set; }

    public ExpenseClaim Claim { get; set; } = null!;

    public ClaimCategory Category { get; set; } = null!;
}

public class ApprovalWorkflow : OrgScopedEntity
{
    public long ApprovalWorkflowId { get; set; }

    [Required(ErrorMessage = "Workflow name is required.")]
    [MaxLength(100, ErrorMessage = "Workflow name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public ClaimRequestKind RequestKind { get; set; } = ClaimRequestKind.Claim;

    public long? DepartmentId { get; set; }

    public long? GradeId { get; set; }

    public long? WorkLocationId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ApprovalWorkflowLevel> Levels { get; set; } = new List<ApprovalWorkflowLevel>();
}

public class ApprovalWorkflowLevel : OrgScopedEntity
{
    public long ApprovalWorkflowLevelId { get; set; }

    public long ApprovalWorkflowId { get; set; }

    public int Sequence { get; set; }

    [Required(ErrorMessage = "Label is required.")]
    [MaxLength(50, ErrorMessage = "Label cannot exceed 50 characters.")]
    public string Label { get; set; } = null!;

    public ApproverKind ApproverKind { get; set; }

    public int? ReportingDepth { get; set; }

    public int? SpecificRoleId { get; set; }

    public long? SpecificEmployeeId { get; set; }

    public ApprovalWorkflow Workflow { get; set; } = null!;
}

public class ApprovalStep : OrgScopedEntity
{
    public long ApprovalStepId { get; set; }

    public long ExpenseClaimId { get; set; }

    public int Sequence { get; set; }

    [Required(ErrorMessage = "Label is required.")]
    [MaxLength(50, ErrorMessage = "Label cannot exceed 50 characters.")]
    public string Label { get; set; } = null!;

    public ApprovalStepStatus StepStatus { get; set; } = ApprovalStepStatus.Pending;

    public long? ApproverEmployeeId { get; set; }

    public Guid? ActedByUserId { get; set; }

    public DateTimeOffset? ActedAt { get; set; }

    [MaxLength(500, ErrorMessage = "Comments cannot exceed 500 characters.")]
    public string? Comments { get; set; }

    public ExpenseClaim Claim { get; set; } = null!;
}

using System.ComponentModel.DataAnnotations;
using Claims.Entity.Enums;

namespace Claims.Entity.Models;

public record ClaimCategoryDto(
    long ClaimCategoryId,
    string Code,
    string Name,
    bool IsReceiptRequired,
    long? LedgerAccountId,
    bool IsTaxable,
    bool IsActive
);

public class CreateClaimCategoryRequest
{
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
}

public record ClaimLimitDto(
    long ClaimLimitId,
    long ClaimCategoryId,
    string CategoryName,
    long? GradeId,
    LimitPeriod LimitPeriod,
    decimal Amount
);

public class SaveClaimLimitRequest
{
    public long ClaimCategoryId { get; set; }

    public long? GradeId { get; set; }

    public LimitPeriod LimitPeriod { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Amount must be positive.")]
    public decimal Amount { get; set; }
}

public record ExpenseClaimLineDto(
    long ExpenseClaimLineId,
    long ExpenseClaimId,
    long ClaimCategoryId,
    string CategoryCode,
    string CategoryName,
    DateOnly ExpenseDate,
    string Description,
    decimal Amount,
    string? ReceiptAttachmentKey
);

public class SaveExpenseClaimLineRequest
{
    public long? ExpenseClaimLineId { get; set; }

    public long ClaimCategoryId { get; set; }

    public DateOnly ExpenseDate { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string Description { get; set; } = null!;

    [Range(0.01, 1000000000, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [MaxLength(500, ErrorMessage = "Receipt attachment key cannot exceed 500 characters.")]
    public string? ReceiptAttachmentKey { get; set; }
}

public record ExpenseClaimDto(
    long ExpenseClaimId,
    string ClaimNo,
    long EmployeeId,
    string? EmployeeCode,
    string? EmployeeName,
    DateOnly ClaimDate,
    decimal TotalAmount,
    decimal ApprovedAmount,
    ClaimStatus ClaimStatus,
    PayoutMode PayoutMode,
    long? PayrollRunId,
    long? SpendMoneyId,
    ApprovalStatus ApprovalStatus,
    string? CurrentStepLabel,
    long? CurrentApproverEmployeeId,
    List<ExpenseClaimLineDto> Lines
);

public class CreateExpenseClaimRequest
{
    public long EmployeeId { get; set; }

    public DateOnly ClaimDate { get; set; }

    public PayoutMode PayoutMode { get; set; } = PayoutMode.Direct;

    public List<SaveExpenseClaimLineRequest> Lines { get; set; } = [];
}

public class UpdateExpenseClaimRequest
{
    public DateOnly ClaimDate { get; set; }

    public PayoutMode PayoutMode { get; set; } = PayoutMode.Direct;

    public List<SaveExpenseClaimLineRequest> Lines { get; set; } = [];
}

public class ActClaimApprovalRequest
{
    [Required(ErrorMessage = "Action is required.")]
    public string Action { get; set; } = "Approve"; // Approve, Reject, SendBack

    public string? Comments { get; set; }
}

public record PendingClaimApprovalDto(
    long ApprovalStepId,
    long ExpenseClaimId,
    string ClaimNo,
    long EmployeeId,
    string? EmployeeName,
    DateOnly ClaimDate,
    decimal TotalAmount,
    string StepLabel,
    int Sequence
);

public class PayoutClaimRequest
{
    public PayoutMode? PayoutMode { get; set; }

    // If Direct (Accounting Spend Money):
    public long? BankAccountId { get; set; }
    public string? PaymentReference { get; set; }

    // If Payroll:
    public long? PayrollRunId { get; set; }
}

// Self-Service requests
public class ApplyClaimSelfRequest
{
    public DateOnly ClaimDate { get; set; }

    public PayoutMode PayoutMode { get; set; } = PayoutMode.Direct;

    public List<SaveExpenseClaimLineRequest> Lines { get; set; } = [];
}

using System.ComponentModel.DataAnnotations;
using Payroll.Entity.Enums;

namespace Payroll.Entity.Models;

public sealed class SaveSalaryComponentRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public ComponentKind Kind { get; set; }

    public SalaryValueType ValueType { get; set; }

    public bool IsTaxable { get; set; }

    [MaxLength(500, ErrorMessage = "Formula cannot exceed 500 characters.")]
    public string? Formula { get; set; }

    public long? LedgerAccountId { get; set; }
}

public sealed class SalaryComponentView
{
    public long SalaryComponentId { get; set; }
    public string Name { get; set; } = null!;
    public ComponentKind Kind { get; set; }
    public SalaryValueType ValueType { get; set; }
    public bool IsTaxable { get; set; }
    public string? Formula { get; set; }
    public long? LedgerAccountId { get; set; }
}

public sealed class SaveSalaryStructureComponentRequest
{
    public long SalaryComponentId { get; set; }
    public SalaryValueType ValueType { get; set; }
    public decimal? FlatAmount { get; set; }
    public decimal? Percentage { get; set; }
}

public sealed class SaveSalaryStructureRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public List<SaveSalaryStructureComponentRequest> Components { get; set; } = [];
}

public sealed class SalaryStructureComponentView
{
    public long SalaryStructureComponentId { get; set; }
    public long SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public ComponentKind Kind { get; set; }
    public SalaryValueType ValueType { get; set; }
    public decimal? FlatAmount { get; set; }
    public decimal? Percentage { get; set; }
}

public sealed class SalaryStructureView
{
    public long SalaryStructureId { get; set; }
    public string Name { get; set; } = null!;
    public List<SalaryStructureComponentView> Components { get; set; } = [];
}

public sealed class SaveEmployeeSalaryRequest
{
    public long EmployeeId { get; set; }
    public long SalaryStructureId { get; set; }
    public decimal AnnualCtc { get; set; }
    public DateOnly EffectiveFrom { get; set; }
}

public sealed class EmployeeSalaryView
{
    public long EmployeeSalaryId { get; set; }
    public long EmployeeId { get; set; }
    public long SalaryStructureId { get; set; }
    public string StructureName { get; set; } = null!;
    public decimal AnnualCtc { get; set; }
    public decimal MonthlyCtc => Math.Round(AnnualCtc / 12m, 2);
    public DateOnly EffectiveFrom { get; set; }
}

public sealed class SaveSalaryRevisionRequest
{
    public long EmployeeId { get; set; }
    public decimal NewCtc { get; set; }
    public DateOnly EffectiveFrom { get; set; }

    [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters.")]
    public string? Reason { get; set; }
}

public sealed class SalaryRevisionView
{
    public long SalaryRevisionId { get; set; }
    public long EmployeeId { get; set; }
    public decimal PreviousCtc { get; set; }
    public decimal NewCtc { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public string? Reason { get; set; }
    public bool ArrearsProcessed { get; set; }
}

public sealed class SaveOneTimePaymentRequest
{
    public long EmployeeId { get; set; }
    public long SalaryComponentId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Month { get; set; }
}

public sealed class OneTimePaymentView
{
    public long OneTimePaymentId { get; set; }
    public long EmployeeId { get; set; }
    public long SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly Month { get; set; }
    public bool Processed { get; set; }
}

public sealed class SaveSalaryHoldRequest
{
    public long EmployeeId { get; set; }
    public DateOnly FromMonth { get; set; }
    public DateOnly? ToMonth { get; set; }

    [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters.")]
    public string? Reason { get; set; }
}

public sealed class SalaryHoldView
{
    public long SalaryHoldId { get; set; }
    public long EmployeeId { get; set; }
    public DateOnly FromMonth { get; set; }
    public DateOnly? ToMonth { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public sealed class DisburseLoanRequest
{
    public long EmployeeId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public int TermMonths { get; set; }
    public decimal MonthlyEmi { get; set; }
    public DateOnly DisbursedDate { get; set; }
    public DateOnly DeductionStartDate { get; set; }
}

public sealed class EmployeeLoanView
{
    public long EmployeeLoanId { get; set; }
    public long EmployeeId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public int TermMonths { get; set; }
    public decimal MonthlyEmi { get; set; }
    public DateOnly DisbursedDate { get; set; }
    public DateOnly DeductionStartDate { get; set; }
    public bool IsSettled { get; set; }
    public decimal TotalRepaid { get; set; }
    public decimal BalanceRemaining { get; set; }
}

public sealed class SaveMonthlyAttendanceRequest
{
    public long EmployeeId { get; set; }
    public DateOnly Month { get; set; }
    public decimal PaidDays { get; set; }
    public decimal LossOfPayDays { get; set; }
}

public sealed class MonthlyAttendanceView
{
    public long MonthlyAttendanceInputId { get; set; }
    public long EmployeeId { get; set; }
    public DateOnly Month { get; set; }
    public decimal PaidDays { get; set; }
    public decimal LossOfPayDays { get; set; }
}

public sealed class ProcessPayrollRunRequest
{
    public DateOnly Month { get; set; }
}

public sealed class PayrollRunView
{
    public long PayrollRunId { get; set; }
    public DateOnly Month { get; set; }
    public PayrollRunStatus Status { get; set; }
    public int EmployeeCount { get; set; }
    public decimal TotalGrossEarnings { get; set; }
    public decimal TotalGrossDeductions { get; set; }
    public decimal TotalNetPay { get; set; }
    public string DaysSource { get; set; } = null!;
    public long? JournalId { get; set; }
}

public sealed class PayslipLineView
{
    public long PayslipLineId { get; set; }
    public long SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public ComponentKind Kind { get; set; }
    public decimal Amount { get; set; }
}

public sealed class PayslipView
{
    public long PayslipId { get; set; }
    public long PayrollRunId { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    public string? EmployeeName { get; set; }
    public decimal PaidDays { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal GrossDeductions { get; set; }
    public decimal NetPay { get; set; }
    public List<PayslipLineView> Lines { get; set; } = [];
}

public sealed class MyPayslipSummaryDto
{
    public long PayslipId { get; set; }
    public long PayrollRunId { get; set; }
    public DateOnly Month { get; set; }
    public string MonthName { get; set; } = null!;
    public decimal PaidDays { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal GrossDeductions { get; set; }
    public decimal NetPay { get; set; }
    public string Status { get; set; } = null!;
}


#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

// -----------------------------------------------------------------------------
// hrm schema read models
// -----------------------------------------------------------------------------

public class EmployeeRecordRead : OrgScopedEntity
{
    public long EmployeeId { get; set; }
    public string EmployeeCode { get; set; }
    public string FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; }
    public string MaritalStatus { get; set; }
    public string? BloodGroup { get; set; }
    public long DepartmentId { get; set; }
    public long DesignationId { get; set; }
    public long GradeId { get; set; }
    public long WorkLocationId { get; set; }
    public long? CostCentreId { get; set; }
    public long? ReportsToEmployeeId { get; set; }
    public DateOnly JoiningDate { get; set; }
    public DateOnly? ProbationEndDate { get; set; }
    public DateOnly? ConfirmationDate { get; set; }
    public int NoticePeriodDays { get; set; }
    public string EmploymentType { get; set; }
    public string EmployeeStatus { get; set; }
    public DateOnly? ExitDate { get; set; }
    public string? Pan { get; set; }
    public string? Uan { get; set; }
    public string Phone { get; set; }
    public string? WorkEmail { get; set; }
}

public class DepartmentRead : OrgScopedEntity
{
    public long DepartmentId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public long? HeadEmployeeId { get; set; }
    public long? ParentDepartmentId { get; set; }
    public bool IsActive { get; set; }
}

public class DesignationRead : OrgScopedEntity
{
    public long DesignationId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
}

public class GradeRead : OrgScopedEntity
{
    public long GradeId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public int SortOrder { get; set; }
    public int NoticePeriodDays { get; set; }
    public bool IsActive { get; set; }
}

public class WorkLocationRead : OrgScopedEntity
{
    public long WorkLocationId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public int? StateId { get; set; }
    public string? City { get; set; }
    public bool IsActive { get; set; }
}

public class CostCentreRead : OrgScopedEntity
{
    public long CostCentreId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
}

public class EmployeeDocumentRead : OrgScopedEntity
{
    public long EmployeeDocumentId { get; set; }
    public long EmployeeId { get; set; }
    public string DocumentKind { get; set; }
    public string AttachmentKey { get; set; }
    public DateOnly? ValidUntil { get; set; }
}

public class EmployeeBankDetailRead : OrgScopedEntity
{
    public long EmployeeBankDetailId { get; set; }
    public long EmployeeId { get; set; }
    public string AccountHolder { get; set; }
    public string AccountNo { get; set; }
    public string Ifsc { get; set; }
    public string BankName { get; set; }
    public bool IsPrimary { get; set; }
}

public class SeparationRead : OrgScopedEntity
{
    public long SeparationId { get; set; }
    public long EmployeeId { get; set; }
    public string Kind { get; set; }
    public DateOnly RequestDate { get; set; }
    public DateOnly LastWorkingDate { get; set; }
    public decimal NoticeShortfallDays { get; set; }
    public bool IsNoticeWaived { get; set; }
    public string Reason { get; set; }
    public string Status { get; set; }
}

// -----------------------------------------------------------------------------
// tla schema read models
// -----------------------------------------------------------------------------

public class DailyAttendanceRead : OrgScopedEntity
{
    public long DailyAttendanceId { get; set; }
    public long EmployeeId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public long? ShiftId { get; set; }
    public DateTimeOffset? FirstIn { get; set; }
    public DateTimeOffset? LastOut { get; set; }
    public int WorkedMinutes { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyOutMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
    public string AttendanceStatus { get; set; }
    public string AttendanceSource { get; set; }
    public bool IsLocked { get; set; }
}

public class ShiftRead : OrgScopedEntity
{
    public long ShiftId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public bool IsActive { get; set; }
}

public class OvertimeRequestRead : OrgScopedEntity
{
    public long OvertimeRequestId { get; set; }
    public long EmployeeId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public int Minutes { get; set; }
    public decimal OvertimeRate { get; set; }
    public string ApprovalStatus { get; set; }
    public string? CurrentStepLabel { get; set; }
}

public class RegularisationRequestRead : OrgScopedEntity
{
    public long RegularisationRequestId { get; set; }
    public long EmployeeId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public DateTimeOffset? RequestedIn { get; set; }
    public DateTimeOffset? RequestedOut { get; set; }
    public string RequestedStatus { get; set; }
    public string Reason { get; set; }
    public string ApprovalStatus { get; set; }
    public string? CurrentStepLabel { get; set; }
}

public class LeaveTypeRead : OrgScopedEntity
{
    public long LeaveTypeId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public bool IsPaid { get; set; }
    public bool IsActive { get; set; }
}

public class LeaveBalanceRead : OrgScopedEntity
{
    public long LeaveBalanceId { get; set; }
    public long EmployeeId { get; set; }
    public long LeaveTypeId { get; set; }
    public int LeaveYear { get; set; }
    public decimal Opening { get; set; }
    public decimal Accrued { get; set; }
    public decimal Taken { get; set; }
    public decimal Encashed { get; set; }
    public decimal Lapsed { get; set; }
    public decimal Adjusted { get; set; }
}

public class LeaveApplicationRead : OrgScopedEntity
{
    public long LeaveApplicationId { get; set; }
    public long EmployeeId { get; set; }
    public long LeaveTypeId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string FromHalf { get; set; }
    public string ToHalf { get; set; }
    public decimal Days { get; set; }
    public string Reason { get; set; }
    public string LeaveStatus { get; set; }
    public string ApprovalStatus { get; set; }
    public string? CurrentStepLabel { get; set; }
}

public class LeaveEncashmentRead : OrgScopedEntity
{
    public long LeaveEncashmentId { get; set; }
    public long EmployeeId { get; set; }
    public long LeaveTypeId { get; set; }
    public int LeaveYear { get; set; }
    public decimal Days { get; set; }
    public long? PayrollRunId { get; set; }
    public string EncashmentStatus { get; set; }
    public string ApprovalStatus { get; set; }
}

// -----------------------------------------------------------------------------
// pay schema read models
// -----------------------------------------------------------------------------

public class PayrollRunRead : OrgScopedEntity
{
    public long PayrollRunId { get; set; }
    public DateOnly Month { get; set; }
    public string Status { get; set; }
    public string Kind { get; set; }
    public int EmployeeCount { get; set; }
    public decimal TotalNetPay { get; set; }
    public string DaysSource { get; set; }
    public long? JournalId { get; set; }
}

public class PayslipRead
{
    public long PayslipId { get; set; }
    public long PayrollRunId { get; set; }
    public long EmployeeId { get; set; }
    public decimal PaidDays { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal GrossDeductions { get; set; }
    public decimal NetPay { get; set; }
}

public class PayslipLineRead
{
    public long PayslipLineId { get; set; }
    public long PayslipId { get; set; }
    public long SalaryComponentId { get; set; }
    public string Kind { get; set; }
    public decimal Amount { get; set; }
}

public class PayGroupRead : OrgScopedEntity
{
    public long PayGroupId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
}

public class SalaryComponentRead : OrgScopedEntity
{
    public long SalaryComponentId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Kind { get; set; }
    public bool IsTaxable { get; set; }
    public bool IsActive { get; set; }
}

public class SalaryStructureRead : OrgScopedEntity
{
    public long SalaryStructureId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
}

public class EmployeeSalaryRead : OrgScopedEntity
{
    public long EmployeeSalaryId { get; set; }
    public long EmployeeId { get; set; }
    public long SalaryStructureId { get; set; }
    public decimal AnnualCtc { get; set; }
    public DateOnly EffectiveFrom { get; set; }
}

public class SalaryHoldRead : OrgScopedEntity
{
    public long SalaryHoldId { get; set; }
    public long PayrollRunId { get; set; }
    public long EmployeeId { get; set; }
    public string Reason { get; set; }
    public string Status { get; set; }
    public DateOnly? ReleasedDate { get; set; }
}

public class OneTimePaymentRead : OrgScopedEntity
{
    public long OneTimePaymentId { get; set; }
    public long EmployeeId { get; set; }
    public long SalaryComponentId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentMonth { get; set; }
    public string? Remarks { get; set; }
}

public class EmployeeLoanRead : OrgScopedEntity
{
    public long EmployeeLoanId { get; set; }
    public long EmployeeId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public DateOnly DisbursedDate { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public string Status { get; set; }
    public decimal TotalRecovered { get; set; }
}

public class TaxDeclarationRead : OrgScopedEntity
{
    public long TaxDeclarationId { get; set; }
    public long EmployeeId { get; set; }
    public string FinancialYear { get; set; }
    public string Regime { get; set; }
    public bool IsLocked { get; set; }
}

// -----------------------------------------------------------------------------
// rec schema read models
// -----------------------------------------------------------------------------

public class JobRequisitionRead : OrgScopedEntity
{
    public long JobRequisitionId { get; set; }
    public string RequisitionCode { get; set; }
    public long DepartmentId { get; set; }
    public long DesignationId { get; set; }
    public long GradeId { get; set; }
    public long WorkLocationId { get; set; }
    public int Openings { get; set; }
    public string EmploymentType { get; set; }
    public decimal MinCtc { get; set; }
    public decimal MaxCtc { get; set; }
    public string ApprovalStatus { get; set; }
}

public class JobOpeningRead : OrgScopedEntity
{
    public long JobOpeningId { get; set; }
    public long JobRequisitionId { get; set; }
    public string Title { get; set; }
    public string OpeningStatus { get; set; }
    public DateOnly? PublishedDate { get; set; }
    public DateOnly? ClosingDate { get; set; }
}

public class CandidateRead : OrgScopedEntity
{
    public long CandidateId { get; set; }
    public string FirstName { get; set; }
    public string? LastName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public decimal? CurrentCtc { get; set; }
    public decimal? ExpectedCtc { get; set; }
    public string CandidateSource { get; set; }
}

public class ApplicationRead : OrgScopedEntity
{
    public long ApplicationId { get; set; }
    public long JobOpeningId { get; set; }
    public long CandidateId { get; set; }
    public string Stage { get; set; }
    public string? RejectionReason { get; set; }
}

public class OfferRead : OrgScopedEntity
{
    public long OfferId { get; set; }
    public long ApplicationId { get; set; }
    public decimal OfferedCtc { get; set; }
    public DateOnly JoiningDate { get; set; }
    public string OfferStatus { get; set; }
    public string ApprovalStatus { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}

// -----------------------------------------------------------------------------
// clm schema read models
// -----------------------------------------------------------------------------

public class ClaimCategoryRead : OrgScopedEntity
{
    public long ClaimCategoryId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public bool IsReceiptRequired { get; set; }
    public bool IsTaxable { get; set; }
    public bool IsActive { get; set; }
}

public class ExpenseClaimRead : OrgScopedEntity
{
    public long ExpenseClaimId { get; set; }
    public string ClaimNo { get; set; }
    public long EmployeeId { get; set; }
    public DateOnly ClaimDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public string ClaimStatus { get; set; }
    public string PayoutMode { get; set; }
    public long? PayrollRunId { get; set; }
    public string ApprovalStatus { get; set; }
    public string? CurrentStepLabel { get; set; }
    public long? CurrentApproverEmployeeId { get; set; }
}

public class ExpenseClaimLineRead : OrgScopedEntity
{
    public long ExpenseClaimLineId { get; set; }
    public long ExpenseClaimId { get; set; }
    public long ClaimCategoryId { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
}

public class ApprovalStepRead : OrgScopedEntity
{
    public long ApprovalStepId { get; set; }
    public long ExpenseClaimId { get; set; }
    public int Sequence { get; set; }
    public string Label { get; set; }
    public string StepStatus { get; set; }
    public long? ApproverEmployeeId { get; set; }
    public string? Comments { get; set; }
}

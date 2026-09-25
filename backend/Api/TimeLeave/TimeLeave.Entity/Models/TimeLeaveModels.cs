using TimeLeave.Entity.Enums;

namespace TimeLeave.Entity.Models;

public record LeaveTypeDto(
    long LeaveTypeId,
    string Code,
    string Name,
    bool IsPaid,
    bool IsHalfDayAllowed,
    decimal? IsAttachmentRequiredAboveDays,
    Gender? Gender,
    bool IsActive
);

public record CreateLeaveTypeRequest(
    string Code,
    string Name,
    bool IsPaid,
    bool IsHalfDayAllowed,
    decimal? IsAttachmentRequiredAboveDays,
    Gender? Gender
);

public record LeavePolicyDto(
    long LeavePolicyId,
    long LeaveTypeId,
    string? LeaveTypeName,
    long? GradeId,
    long? WorkLocationId,
    DateOnly EffectiveFrom,
    decimal AnnualQuota,
    AccrualKind AccrualKind,
    bool IsProratedOnJoining,
    CarryForwardKind CarryForwardKind,
    decimal? MaxCarryForward,
    decimal? MaxEncashPerYear,
    decimal? MinDaysPerApplication,
    decimal? MaxDaysPerApplication,
    int NoticeDays,
    bool IsSandwichRule,
    bool CanApplyInProbation
);

public record CreateLeavePolicyRequest(
    long LeaveTypeId,
    long? GradeId,
    long? WorkLocationId,
    DateOnly EffectiveFrom,
    decimal AnnualQuota,
    AccrualKind AccrualKind,
    bool IsProratedOnJoining,
    CarryForwardKind CarryForwardKind,
    decimal? MaxCarryForward,
    decimal? MaxEncashPerYear,
    decimal? MinDaysPerApplication,
    decimal? MaxDaysPerApplication,
    int NoticeDays,
    bool IsSandwichRule,
    bool CanApplyInProbation
);

public record LeaveBalanceDto(
    long LeaveBalanceId,
    long EmployeeId,
    long LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    int LeaveYear,
    decimal Opening,
    decimal Accrued,
    decimal Taken,
    decimal Encashed,
    decimal Lapsed,
    decimal Adjusted,
    decimal Available
);

public record ApplyLeaveRequest(
    long EmployeeId,
    long LeaveTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    LeaveHalf FromHalf,
    LeaveHalf ToHalf,
    string Reason,
    string? AttachmentKey,
    long? ReportsToEmployeeId = null
);

public record LeaveApplicationDto(
    long LeaveApplicationId,
    long EmployeeId,
    long LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    DateOnly FromDate,
    DateOnly ToDate,
    LeaveHalf FromHalf,
    LeaveHalf ToHalf,
    decimal Days,
    string Reason,
    string? AttachmentKey,
    LeaveStatus LeaveStatus,
    ApprovalStatus ApprovalStatus,
    string? CurrentStepLabel,
    long? CurrentApproverEmployeeId
);

public record ApplyEncashmentRequest(
    long EmployeeId,
    long LeaveTypeId,
    int LeaveYear,
    decimal Days
);

public record LeaveEncashmentDto(
    long LeaveEncashmentId,
    long EmployeeId,
    long LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    int LeaveYear,
    decimal Days,
    EncashmentStatus EncashmentStatus,
    ApprovalStatus ApprovalStatus,
    string? CurrentStepLabel
);

public record ShiftDto(
    long ShiftId,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    int HalfDayBelowMinutes,
    int AbsentBelowMinutes,
    bool IsNightShift,
    bool IsActive
);

public record CreateShiftRequest(
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    int HalfDayBelowMinutes,
    int AbsentBelowMinutes,
    bool IsNightShift
);

public record WeeklyOffPolicyDto(
    long WeeklyOffPolicyId,
    string Name,
    WeeklyOffKind MondayRule,
    WeeklyOffKind TuesdayRule,
    WeeklyOffKind WednesdayRule,
    WeeklyOffKind ThursdayRule,
    WeeklyOffKind FridayRule,
    WeeklyOffKind SaturdayRule,
    WeeklyOffKind SundayRule,
    string? AlternateWeeks,
    bool IsActive
);

public record CreateWeeklyOffPolicyRequest(
    string Name,
    WeeklyOffKind MondayRule,
    WeeklyOffKind TuesdayRule,
    WeeklyOffKind WednesdayRule,
    WeeklyOffKind ThursdayRule,
    WeeklyOffKind FridayRule,
    WeeklyOffKind SaturdayRule,
    WeeklyOffKind SundayRule,
    string? AlternateWeeks
);

public record HolidayListDto(
    long HolidayListId,
    string Name,
    long WorkLocationId,
    int CalendarYear,
    int MaxOptionalPerYear,
    List<HolidayDto> Holidays
);

public record CreateHolidayListRequest(
    string Name,
    long WorkLocationId,
    int CalendarYear,
    int MaxOptionalPerYear
);

public record HolidayDto(
    long HolidayId,
    long HolidayListId,
    DateOnly HolidayDate,
    string Name,
    bool IsOptional
);

public record CreateHolidayRequest(
    DateOnly HolidayDate,
    string Name,
    bool IsOptional
);

public record ShiftRosterDto(
    long ShiftRosterId,
    long EmployeeId,
    DateOnly FromDate,
    DateOnly ToDate,
    long ShiftId,
    string? ShiftName,
    long WeeklyOffPolicyId,
    string? WeeklyOffPolicyName
);

public record AssignRosterRequest(
    long EmployeeId,
    DateOnly FromDate,
    DateOnly ToDate,
    long ShiftId,
    long WeeklyOffPolicyId
);

public record PunchDto(
    long PunchId,
    long EmployeeId,
    DateTimeOffset PunchedAt,
    PunchSource PunchSource,
    string? DeviceCode,
    decimal? Latitude,
    decimal? Longitude,
    bool? IsInsideFence
);

public record RecordPunchRequest(
    long EmployeeId,
    DateTimeOffset PunchedAt,
    PunchSource PunchSource,
    string? DeviceCode,
    decimal? Latitude,
    decimal? Longitude
);

public record BiometricPunchImportItem(
    string DeviceUserId,
    DateTimeOffset PunchedAt,
    string? DeviceCode
);

public record BiometricPunchImportRequest(
    string DeviceCode,
    List<BiometricPunchImportItem> Punches
);

public record DailyAttendanceDto(
    long DailyAttendanceId,
    long EmployeeId,
    DateOnly AttendanceDate,
    long? ShiftId,
    string? ShiftName,
    DateTimeOffset? FirstIn,
    DateTimeOffset? LastOut,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyOutMinutes,
    int OvertimeMinutes,
    AttendanceStatus AttendanceStatus,
    AttendanceSource AttendanceSource,
    bool IsLocked
);

public record CreateRegularisationRequest(
    long EmployeeId,
    DateOnly AttendanceDate,
    DateTimeOffset? RequestedIn,
    DateTimeOffset? RequestedOut,
    AttendanceStatus RequestedStatus,
    string Reason,
    long? ReportsToEmployeeId = null
);

public record RegularisationRequestDto(
    long RegularisationRequestId,
    long EmployeeId,
    DateOnly AttendanceDate,
    DateTimeOffset? RequestedIn,
    DateTimeOffset? RequestedOut,
    AttendanceStatus RequestedStatus,
    string Reason,
    ApprovalStatus ApprovalStatus,
    string? CurrentStepLabel
);

public record CreateOvertimeRequest(
    long EmployeeId,
    DateOnly AttendanceDate,
    int Minutes,
    decimal OvertimeRate,
    long? ReportsToEmployeeId = null
);

public record OvertimeRequestDto(
    long OvertimeRequestId,
    long EmployeeId,
    DateOnly AttendanceDate,
    int Minutes,
    decimal OvertimeRate,
    ApprovalStatus ApprovalStatus,
    string? CurrentStepLabel
);

public record CompOffCreditDto(
    long CompOffCreditId,
    long EmployeeId,
    DateOnly EarnedDate,
    decimal Days,
    DateOnly ExpiryDate,
    decimal AvailedDays,
    decimal AvailableDays
);

public record ApprovalActionRequest(
    ApprovalStepStatus Decision,
    string? Comments
);

public record MonthlyPaidDaysDto(
    long EmployeeId,
    int Year,
    int Month,
    int TotalDays,
    decimal PresentDays,
    decimal HalfDays,
    decimal AbsentDays,
    decimal PaidLeaveDays,
    decimal UnpaidLeaveDays,
    decimal HolidayDays,
    decimal WeeklyOffDays,
    decimal TotalPaidDays
);

public class ApplyLeaveSelfRequest
{
    public long LeaveTypeId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public bool FromHalf { get; set; }
    public bool ToHalf { get; set; }
    public string? Reason { get; set; }
    public string? AttachmentKey { get; set; }
}

public class MobilePunchSelfRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Notes { get; set; }
}

public class SubmitRegularisationSelfRequest
{
    public DateOnly AttendanceDate { get; set; }
    public DateTimeOffset? RequestedIn { get; set; }
    public DateTimeOffset? RequestedOut { get; set; }
    public AttendanceStatus RequestedStatus { get; set; } = AttendanceStatus.Present;
    public string Reason { get; set; } = string.Empty;
}

public class SubmitOvertimeSelfRequest
{
    public DateOnly AttendanceDate { get; set; }
    public int Minutes { get; set; }
    public decimal OvertimeRate { get; set; } = 1.5m;
}

public class ActApprovalRequest
{
    public string Action { get; set; } = "Approve"; // Approve, Reject, SendBack
    public string? Comments { get; set; }
}

public class PendingApprovalItemDto
{
    public long ApprovalStepId { get; set; }
    public int Sequence { get; set; }
    public string Label { get; set; } = null!;
    public string RequestKind { get; set; } = null!;
    public long RequestId { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? Reason { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public decimal? Days { get; set; }
    public DateOnly? AttendanceDate { get; set; }
    public int? OvertimeMinutes { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}


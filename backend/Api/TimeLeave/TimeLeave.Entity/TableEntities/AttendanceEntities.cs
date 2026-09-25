using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;
using TimeLeave.Entity.Enums;

namespace TimeLeave.Entity.TableEntities;

public class HolidayList : OrgScopedEntity
{
    [Key]
    public long HolidayListId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public long WorkLocationId { get; set; }

    public int CalendarYear { get; set; }

    public int MaxOptionalPerYear { get; set; }

    public ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();
}

public class Holiday : OrgScopedEntity
{
    [Key]
    public long HolidayId { get; set; }

    public long HolidayListId { get; set; }

    public HolidayList? HolidayList { get; set; }

    public DateOnly HolidayDate { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public bool IsOptional { get; set; }
}

public class Shift : OrgScopedEntity
{
    [Key]
    public long ShiftId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int BreakMinutes { get; set; }

    public int GraceInMinutes { get; set; }

    public int GraceOutMinutes { get; set; }

    public int HalfDayBelowMinutes { get; set; }

    public int AbsentBelowMinutes { get; set; }

    public bool IsNightShift { get; set; }

    public bool IsActive { get; set; } = true;
}

public class WeeklyOffPolicy : OrgScopedEntity
{
    [Key]
    public long WeeklyOffPolicyId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public WeeklyOffKind MondayRule { get; set; } = WeeklyOffKind.Working;

    public WeeklyOffKind TuesdayRule { get; set; } = WeeklyOffKind.Working;

    public WeeklyOffKind WednesdayRule { get; set; } = WeeklyOffKind.Working;

    public WeeklyOffKind ThursdayRule { get; set; } = WeeklyOffKind.Working;

    public WeeklyOffKind FridayRule { get; set; } = WeeklyOffKind.Working;

    public WeeklyOffKind SaturdayRule { get; set; } = WeeklyOffKind.AlternateOff;

    public WeeklyOffKind SundayRule { get; set; } = WeeklyOffKind.Off;

    [MaxLength(20, ErrorMessage = "Alternate weeks cannot exceed 20 characters.")]
    public string? AlternateWeeks { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ShiftRoster : OrgScopedEntity
{
    [Key]
    public long ShiftRosterId { get; set; }

    public long EmployeeId { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public long ShiftId { get; set; }

    public Shift? Shift { get; set; }

    public long WeeklyOffPolicyId { get; set; }

    public WeeklyOffPolicy? WeeklyOffPolicy { get; set; }
}

public class Punch : OrgScopedEntity
{
    [Key]
    public long PunchId { get; set; }

    public long EmployeeId { get; set; }

    public DateTimeOffset PunchedAt { get; set; }

    public PunchSource PunchSource { get; set; } = PunchSource.Biometric;

    [MaxLength(50, ErrorMessage = "Device code cannot exceed 50 characters.")]
    public string? DeviceCode { get; set; }

    [Column(TypeName = "decimal(9,6)")]
    public decimal? Latitude { get; set; }

    [Column(TypeName = "decimal(9,6)")]
    public decimal? Longitude { get; set; }

    public bool? IsInsideFence { get; set; }
}

public class BiometricDeviceUser : OrgScopedEntity
{
    [Key]
    public long BiometricDeviceUserId { get; set; }

    [Required(ErrorMessage = "Device code is required.")]
    [MaxLength(50, ErrorMessage = "Device code cannot exceed 50 characters.")]
    public string DeviceCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Device user id is required.")]
    [MaxLength(30, ErrorMessage = "Device user id cannot exceed 30 characters.")]
    public string DeviceUserId { get; set; } = string.Empty;

    public long EmployeeId { get; set; }
}

public class DailyAttendance : OrgScopedEntity
{
    [Key]
    public long DailyAttendanceId { get; set; }

    public long EmployeeId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public long? ShiftId { get; set; }

    public Shift? Shift { get; set; }

    public DateTimeOffset? FirstIn { get; set; }

    public DateTimeOffset? LastOut { get; set; }

    public int WorkedMinutes { get; set; }

    public int LateMinutes { get; set; }

    public int EarlyOutMinutes { get; set; }

    public int OvertimeMinutes { get; set; }

    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.Present;

    public AttendanceSource AttendanceSource { get; set; } = AttendanceSource.Derived;

    public bool IsLocked { get; set; }
}

public class RegularisationRequest : OrgScopedEntity
{
    [Key]
    public long RegularisationRequestId { get; set; }

    public long EmployeeId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public DateTimeOffset? RequestedIn { get; set; }

    public DateTimeOffset? RequestedOut { get; set; }

    public AttendanceStatus RequestedStatus { get; set; } = AttendanceStatus.Present;

    [Required(ErrorMessage = "Reason is required.")]
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = string.Empty;

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    [MaxLength(50, ErrorMessage = "Step label cannot exceed 50 characters.")]
    public string? CurrentStepLabel { get; set; }

    public long? CurrentApproverEmployeeId { get; set; }
}

public class OvertimeRequest : OrgScopedEntity
{
    [Key]
    public long OvertimeRequestId { get; set; }

    public long EmployeeId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public int Minutes { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal OvertimeRate { get; set; } = 1.5m;

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    [MaxLength(50, ErrorMessage = "Step label cannot exceed 50 characters.")]
    public string? CurrentStepLabel { get; set; }

    public long? CurrentApproverEmployeeId { get; set; }
}

public class CompOffCredit : OrgScopedEntity
{
    [Key]
    public long CompOffCreditId { get; set; }

    public long EmployeeId { get; set; }

    public DateOnly EarnedDate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Days { get; set; } = 1m;

    public DateOnly ExpiryDate { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal AvailedDays { get; set; }
}

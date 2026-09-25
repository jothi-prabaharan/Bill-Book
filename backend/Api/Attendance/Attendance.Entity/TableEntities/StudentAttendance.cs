using System.ComponentModel.DataAnnotations;
using Attendance.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Attendance.Entity.TableEntities;

/// <summary>
/// One student's mark for one day (S3, TK-63). Student attendance only: staff
/// attendance is HRMS's. The section and enrolment are Sis's, unenforced and
/// checked against the section's roll when the register is saved.
/// </summary>
public class StudentAttendance : OrgScopedEntity
{
    public long StudentAttendanceId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    /// <summary>Unenforced: <c>sis.Sections</c>.</summary>
    public long SectionId { get; set; }

    /// <summary>Unenforced: <c>sis.Enrolments</c>. One mark per enrolment per day.</summary>
    public long EnrolmentId { get; set; }

    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.Present;

    [MaxLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
    public string? Remarks { get; set; }
}

/// <summary>A section's day, closed. A locked day refuses changes except from a holder of <c>attendance.unlock</c>.</summary>
public class AttendanceLock : OrgScopedEntity
{
    public long AttendanceLockId { get; set; }

    public long SectionId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public bool IsLocked { get; set; }
}

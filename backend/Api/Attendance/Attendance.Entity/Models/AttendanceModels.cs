using System.ComponentModel.DataAnnotations;
using Attendance.Entity.Enums;

namespace Attendance.Entity.Models;

public sealed record AttendanceMessage(string Message);

/// <summary>A section's register for one day: every student on the roll, with a mark where one is saved.</summary>
public sealed class RegisterView
{
    public long SectionId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public bool IsLocked { get; set; }

    /// <summary>Whether any mark has been saved for the day yet.</summary>
    public bool IsTaken { get; set; }

    public List<RegisterRow> Rows { get; set; } = [];
}

public sealed class RegisterRow
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a student.")]
    public long EnrolmentId { get; set; }

    public int? RollNo { get; set; }

    public string? AdmissionNo { get; set; }

    public string? StudentName { get; set; }

    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.Present;

    [MaxLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
    public string? Remarks { get; set; }
}

public sealed class SaveRegisterRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a section.")]
    public long SectionId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public List<RegisterRow> Rows { get; set; } = [];
}

public sealed class LockRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a section.")]
    public long SectionId { get; set; }

    public DateOnly AttendanceDate { get; set; }
}

/// <summary>A student's days by mark, over a range: the portal's and the report card's summary.</summary>
public sealed class AttendanceSummary
{
    public long EnrolmentId { get; set; }

    public int Present { get; set; }

    public int Absent { get; set; }

    public int Late { get; set; }

    public int HalfDay { get; set; }

    public int Leave { get; set; }

    public int Holiday { get; set; }
}

// ---- Parent portal (S9, TK-69) ------------------------------------------------

/// <summary>One child's attendance for a month, as the parent portal shows it.</summary>
public sealed class PortalAttendanceView
{
    public long StudentId { get; set; }

    /// <summary>The first day of the month shown.</summary>
    public DateOnly Month { get; set; }

    public List<PortalAttendanceDay> Days { get; set; } = [];

    public int Present { get; set; }

    public int Absent { get; set; }

    public int Late { get; set; }

    public int HalfDay { get; set; }

    public int Leave { get; set; }

    public int Holiday { get; set; }
}

public sealed class PortalAttendanceDay
{
    public DateOnly AttendanceDate { get; set; }

    public AttendanceStatus AttendanceStatus { get; set; }

    public string? Remarks { get; set; }
}

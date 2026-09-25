namespace Attendance.Entity.Enums;

/// <summary>A student's mark for a day (S3, TK-63), stored by name.</summary>
public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Late = 3,
    HalfDay = 4,
    Leave = 5,
    Holiday = 6,
}

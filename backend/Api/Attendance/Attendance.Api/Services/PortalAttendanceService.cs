using Attendance.Entity.Enums;
using Attendance.Entity.Models;
using Attendance.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.School;

namespace Attendance.Api.Services;

public enum PortalAttendanceOutcome
{
    Ok = 1,

    /// <summary>Not this guardian's child, or not one whose link grants portal access.</summary>
    NotFound = 2,

    /// <summary>Sis could not be asked whose child it is.</summary>
    Unavailable = 3,
}

/// <summary>
/// A child's attendance in the parent portal (S9, TK-69). Attendance knows
/// enrolments, not guardians, so Sis is asked which of the child's enrolments
/// belong to a guardian with portal access, and only those days are shown.
/// </summary>
public sealed class PortalAttendanceService
{
    private readonly AttendanceDbContext _db;
    private readonly ISisClient _sis;
    private readonly ILogger<PortalAttendanceService> _log;

    public PortalAttendanceService(AttendanceDbContext db, ISisClient sis, ILogger<PortalAttendanceService> log)
    {
        _db = db;
        _sis = sis;
        _log = log;
    }

    public async Task<(PortalAttendanceOutcome Outcome, PortalAttendanceView? View)> MonthAsync(
        long contactId, long studentId, DateOnly month, CancellationToken ct)
    {
        List<long> enrolmentIds;
        try
        {
            IReadOnlyList<EnrolmentInfo> theirs = await _sis.EnrolmentsAsync(
                new EnrolmentQueryRequest { GuardianContactId = contactId, PortalAccessOnly = true }, ct);
            enrolmentIds = [.. theirs.Where(e => e.StudentId == studentId).Select(e => e.EnrolmentId)];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "Sis could not say whose child student {StudentId} is.", studentId);
            return (PortalAttendanceOutcome.Unavailable, null);
        }

        if (enrolmentIds.Count == 0)
        {
            return (PortalAttendanceOutcome.NotFound, null);
        }

        var first = new DateOnly(month.Year, month.Month, 1);
        DateOnly last = first.AddMonths(1).AddDays(-1);

        List<PortalAttendanceDay> days = await _db.StudentAttendance.AsNoTracking()
            .Where(a => enrolmentIds.Contains(a.EnrolmentId) && a.AttendanceDate >= first && a.AttendanceDate <= last)
            .OrderBy(a => a.AttendanceDate)
            .Select(a => new PortalAttendanceDay { AttendanceDate = a.AttendanceDate, AttendanceStatus = a.AttendanceStatus, Remarks = a.Remarks })
            .ToListAsync(ct);

        return (PortalAttendanceOutcome.Ok, Summarise(studentId, first, days));
    }

    public static PortalAttendanceView Summarise(long studentId, DateOnly month, List<PortalAttendanceDay> days)
    {
        int Count(AttendanceStatus s) => days.Count(d => d.AttendanceStatus == s);
        return new PortalAttendanceView
        {
            StudentId = studentId,
            Month = month,
            Days = days,
            Present = Count(AttendanceStatus.Present),
            Absent = Count(AttendanceStatus.Absent),
            Late = Count(AttendanceStatus.Late),
            HalfDay = Count(AttendanceStatus.HalfDay),
            Leave = Count(AttendanceStatus.Leave),
            Holiday = Count(AttendanceStatus.Holiday),
        };
    }
}

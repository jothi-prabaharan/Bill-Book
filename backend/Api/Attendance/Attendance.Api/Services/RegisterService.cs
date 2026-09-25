using Attendance.Entity.Enums;
using Attendance.Entity.Models;
using Attendance.Entity.TableEntities;
using Attendance.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.School;

namespace Attendance.Api.Services;

public enum RegisterOutcome
{
    Ok = 1,
    NotFound = 2,
    Invalid = 3,

    /// <summary>The day is locked and the caller does not hold <c>attendance.unlock</c>.</summary>
    Locked = 4,

    /// <summary>Student could not be asked for the roll.</summary>
    Unavailable = 5,
}

public sealed record RegisterResult(RegisterOutcome Outcome, string? Detail = null);

/// <summary>
/// The daily register per section (S3, TK-63).
///
/// <list type="bullet">
/// <item>The roll is Student's, read at the moment of use: a student enrolled
/// today appears today, and a mark is accepted only for someone on the roll.</item>
/// <item>A day is inside the section's school year, never in the future, and
/// never in a closed year.</item>
/// <item><b>A locked day refuses every change</b> — saving marks or locking
/// again — unless the caller holds <c>attendance.unlock</c>, which also
/// reopens it.</item>
/// </list>
/// </summary>
public sealed class RegisterService
{
    public const string UnlockPermission = "attendance.unlock";

    private readonly AttendanceDbContext _db;
    private readonly IStudentClient _sis;
    private readonly ICallerPermissions _caller;
    private readonly TimeProvider _clock;
    private readonly ILogger<RegisterService> _log;

    public RegisterService(AttendanceDbContext db, IStudentClient sis, ICallerPermissions caller, TimeProvider clock, ILogger<RegisterService> log)
    {
        _db = db;
        _sis = sis;
        _caller = caller;
        _clock = clock;
        _log = log;
    }

    public async Task<(RegisterResult Result, RegisterView? View)> GetAsync(long sectionId, DateOnly date, CancellationToken ct)
    {
        SectionRollResponse roll;
        try
        {
            roll = await _sis.RollAsync(sectionId, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "The roll of section {SectionId} could not be read.", sectionId);
            return (new RegisterResult(RegisterOutcome.Unavailable), null);
        }

        if (!roll.SectionExists)
        {
            return (new RegisterResult(RegisterOutcome.NotFound), null);
        }

        Dictionary<long, StudentAttendance> marks = await _db.StudentAttendance.AsNoTracking()
            .Where(a => a.SectionId == sectionId && a.AttendanceDate == date)
            .ToDictionaryAsync(a => a.EnrolmentId, ct);
        bool locked = await IsLockedAsync(sectionId, date, ct);

        return (new RegisterResult(RegisterOutcome.Ok), new RegisterView
        {
            SectionId = sectionId,
            AttendanceDate = date,
            IsLocked = locked,
            IsTaken = marks.Count > 0,
            Rows = [.. roll.Roll.Select(r => new RegisterRow
            {
                EnrolmentId = r.EnrolmentId,
                RollNo = r.RollNo,
                AdmissionNo = r.AdmissionNo,
                StudentName = r.FullName,
                AttendanceStatus = marks.TryGetValue(r.EnrolmentId, out StudentAttendance? m) ? m.AttendanceStatus : AttendanceStatus.Present,
                Remarks = m?.Remarks,
            })],
        });
    }

    public async Task<RegisterResult> SaveAsync(SaveRegisterRequest request, CancellationToken ct)
    {
        (RegisterResult check, SectionRollResponse? roll) = await CheckDayAsync(request.SectionId, request.AttendanceDate, ct);
        if (roll is null)
        {
            return check;
        }

        if (!MayChange(await IsLockedAsync(request.SectionId, request.AttendanceDate, ct), _caller.Has(UnlockPermission)))
        {
            return new RegisterResult(RegisterOutcome.Locked, "This day is locked. Ask someone who can unlock attendance to change it.");
        }

        HashSet<long> onRoll = [.. roll.Roll.Select(r => r.EnrolmentId)];
        if (request.Rows.Any(r => !onRoll.Contains(r.EnrolmentId)))
        {
            return new RegisterResult(RegisterOutcome.Invalid, "Marks can be saved only for students on the section's roll.");
        }

        if (request.Rows.Select(r => r.EnrolmentId).Distinct().Count() != request.Rows.Count)
        {
            return new RegisterResult(RegisterOutcome.Invalid, "A student is marked twice.");
        }

        HashSet<long> ids = [.. request.Rows.Select(r => r.EnrolmentId)];
        Dictionary<long, StudentAttendance> existing = await _db.StudentAttendance
            .Where(a => a.AttendanceDate == request.AttendanceDate && ids.Contains(a.EnrolmentId))
            .ToDictionaryAsync(a => a.EnrolmentId, ct);

        foreach (RegisterRow row in request.Rows)
        {
            if (!existing.TryGetValue(row.EnrolmentId, out StudentAttendance? mark))
            {
                mark = new StudentAttendance { EnrolmentId = row.EnrolmentId, AttendanceDate = request.AttendanceDate };
                _db.StudentAttendance.Add(mark);
            }

            // A student who moved section keeps one mark for the day, under the section it was taken in.
            mark.SectionId = request.SectionId;
            mark.AttendanceStatus = row.AttendanceStatus;
            mark.Remarks = string.IsNullOrWhiteSpace(row.Remarks) ? null : row.Remarks.Trim();
        }

        await _db.SaveChangesAsync(ct);
        return new RegisterResult(RegisterOutcome.Ok);
    }

    /// <summary>Closes a day. Locking an already-locked day is a no-op, not an error.</summary>
    public async Task<RegisterResult> LockAsync(LockRequest request, CancellationToken ct)
    {
        (RegisterResult check, SectionRollResponse? roll) = await CheckDayAsync(request.SectionId, request.AttendanceDate, ct);
        if (roll is null)
        {
            return check;
        }

        if (!await _db.StudentAttendance.AnyAsync(a => a.SectionId == request.SectionId && a.AttendanceDate == request.AttendanceDate, ct))
        {
            return new RegisterResult(RegisterOutcome.Invalid, "Take the register before locking the day.");
        }

        await SetLockAsync(request.SectionId, request.AttendanceDate, true, ct);
        return new RegisterResult(RegisterOutcome.Ok);
    }

    /// <summary>Reopens a locked day. The route demands <c>attendance.unlock</c>.</summary>
    public async Task<RegisterResult> UnlockAsync(LockRequest request, CancellationToken ct)
    {
        if (!await IsLockedAsync(request.SectionId, request.AttendanceDate, ct))
        {
            return new RegisterResult(RegisterOutcome.Ok);
        }

        await SetLockAsync(request.SectionId, request.AttendanceDate, false, ct);
        return new RegisterResult(RegisterOutcome.Ok);
    }

    /// <summary>A student's marks by kind over a range, for the portal and report cards.</summary>
    public async Task<List<AttendanceSummary>> SummaryAsync(IReadOnlyCollection<long> enrolmentIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var counts = await _db.StudentAttendance.AsNoTracking()
            .Where(a => enrolmentIds.Contains(a.EnrolmentId) && a.AttendanceDate >= from && a.AttendanceDate <= to)
            .GroupBy(a => new { a.EnrolmentId, a.AttendanceStatus })
            .Select(g => new { g.Key.EnrolmentId, g.Key.AttendanceStatus, Days = g.Count() })
            .ToListAsync(ct);

        return [.. enrolmentIds.Select(id =>
        {
            int Of(AttendanceStatus s) => counts.Where(c => c.EnrolmentId == id && c.AttendanceStatus == s).Sum(c => c.Days);
            return new AttendanceSummary
            {
                EnrolmentId = id,
                Present = Of(AttendanceStatus.Present),
                Absent = Of(AttendanceStatus.Absent),
                Late = Of(AttendanceStatus.Late),
                HalfDay = Of(AttendanceStatus.HalfDay),
                Leave = Of(AttendanceStatus.Leave),
                Holiday = Of(AttendanceStatus.Holiday),
            };
        })];
    }

    // ---- Rules, pure and public for tests -----------------------------------

    /// <summary>A day may change when it is open, or when the caller may unlock it.</summary>
    public static bool MayChange(bool isLocked, bool mayUnlock) => !isLocked || mayUnlock;

    /// <summary>Why a day cannot take attendance, or null: inside the year, not in the future, the year open.</summary>
    public static string? DayProblem(DateOnly date, DateOnly today, DateOnly yearStart, DateOnly yearEnd, bool yearIsClosed)
    {
        if (date > today)
        {
            return "Attendance cannot be taken for a day that has not come yet.";
        }

        if (date < yearStart || date > yearEnd)
        {
            return "That day is outside the section's school year.";
        }

        return yearIsClosed ? "That school year is closed." : null;
    }

    // ---- Helpers ---------------------------------------------------------------

    private async Task<(RegisterResult Result, SectionRollResponse? Roll)> CheckDayAsync(long sectionId, DateOnly date, CancellationToken ct)
    {
        SectionRollResponse roll;
        try
        {
            roll = await _sis.RollAsync(sectionId, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "The roll of section {SectionId} could not be read.", sectionId);
            return (new RegisterResult(RegisterOutcome.Unavailable), null);
        }

        if (!roll.SectionExists)
        {
            return (new RegisterResult(RegisterOutcome.NotFound), null);
        }

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        return DayProblem(date, today, roll.YearStart, roll.YearEnd, roll.YearIsClosed) is string problem
            ? (new RegisterResult(RegisterOutcome.Invalid, problem), null)
            : (new RegisterResult(RegisterOutcome.Ok), roll);
    }

    private Task<bool> IsLockedAsync(long sectionId, DateOnly date, CancellationToken ct) =>
        _db.AttendanceLocks.AnyAsync(l => l.SectionId == sectionId && l.AttendanceDate == date && l.IsLocked, ct);

    private async Task SetLockAsync(long sectionId, DateOnly date, bool locked, CancellationToken ct)
    {
        AttendanceLock? row = await _db.AttendanceLocks.FirstOrDefaultAsync(l => l.SectionId == sectionId && l.AttendanceDate == date, ct);
        if (row is null)
        {
            row = new AttendanceLock { SectionId = sectionId, AttendanceDate = date };
            _db.AttendanceLocks.Add(row);
        }

        row.IsLocked = locked;
        await _db.SaveChangesAsync(ct);
    }
}

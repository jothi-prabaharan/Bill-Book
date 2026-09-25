using Microsoft.EntityFrameworkCore;
using TimeLeave.Entity.Enums;
using TimeLeave.Entity.Models;
using TimeLeave.Entity.TableEntities;
using TimeLeave.Repository;

namespace TimeLeave.Api.Services;

public class AttendanceService
{
    private readonly TimeLeaveDbContext _db;
    private readonly LeaveCalculationEngine _leaveEngine;

    public AttendanceService(TimeLeaveDbContext db, LeaveCalculationEngine leaveEngine)
    {
        _db = db;
        _leaveEngine = leaveEngine;
    }

    public async Task<List<ShiftDto>> GetShiftsAsync(CancellationToken ct = default)
    {
        return await _db.Shifts
            .AsNoTracking()
            .Select(s => new ShiftDto(
                s.ShiftId,
                s.Code,
                s.Name,
                s.StartTime,
                s.EndTime,
                s.BreakMinutes,
                s.GraceInMinutes,
                s.GraceOutMinutes,
                s.HalfDayBelowMinutes,
                s.AbsentBelowMinutes,
                s.IsNightShift,
                s.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ShiftDto> CreateShiftAsync(CreateShiftRequest req, CancellationToken ct = default)
    {
        var shift = new Shift
        {
            Code = req.Code.Trim().ToUpperInvariant(),
            Name = req.Name.Trim(),
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            BreakMinutes = req.BreakMinutes,
            GraceInMinutes = req.GraceInMinutes,
            GraceOutMinutes = req.GraceOutMinutes,
            HalfDayBelowMinutes = req.HalfDayBelowMinutes,
            AbsentBelowMinutes = req.AbsentBelowMinutes,
            IsNightShift = req.IsNightShift,
            IsActive = true
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(ct);

        return new ShiftDto(
            shift.ShiftId,
            shift.Code,
            shift.Name,
            shift.StartTime,
            shift.EndTime,
            shift.BreakMinutes,
            shift.GraceInMinutes,
            shift.GraceOutMinutes,
            shift.HalfDayBelowMinutes,
            shift.AbsentBelowMinutes,
            shift.IsNightShift,
            shift.IsActive);
    }

    public async Task<List<WeeklyOffPolicyDto>> GetWeeklyOffPoliciesAsync(CancellationToken ct = default)
    {
        return await _db.WeeklyOffPolicies
            .AsNoTracking()
            .Select(w => new WeeklyOffPolicyDto(
                w.WeeklyOffPolicyId,
                w.Name,
                w.MondayRule,
                w.TuesdayRule,
                w.WednesdayRule,
                w.ThursdayRule,
                w.FridayRule,
                w.SaturdayRule,
                w.SundayRule,
                w.AlternateWeeks,
                w.IsActive))
            .ToListAsync(ct);
    }

    public async Task<WeeklyOffPolicyDto> CreateWeeklyOffPolicyAsync(CreateWeeklyOffPolicyRequest req, CancellationToken ct = default)
    {
        var policy = new WeeklyOffPolicy
        {
            Name = req.Name.Trim(),
            MondayRule = req.MondayRule,
            TuesdayRule = req.TuesdayRule,
            WednesdayRule = req.WednesdayRule,
            ThursdayRule = req.ThursdayRule,
            FridayRule = req.FridayRule,
            SaturdayRule = req.SaturdayRule,
            SundayRule = req.SundayRule,
            AlternateWeeks = req.AlternateWeeks,
            IsActive = true
        };

        _db.WeeklyOffPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);

        return new WeeklyOffPolicyDto(
            policy.WeeklyOffPolicyId,
            policy.Name,
            policy.MondayRule,
            policy.TuesdayRule,
            policy.WednesdayRule,
            policy.ThursdayRule,
            policy.FridayRule,
            policy.SaturdayRule,
            policy.SundayRule,
            policy.AlternateWeeks,
            policy.IsActive);
    }

    public async Task<List<HolidayListDto>> GetHolidayListsAsync(int year, CancellationToken ct = default)
    {
        return await _db.HolidayLists
            .AsNoTracking()
            .Include(h => h.Holidays)
            .Where(h => h.CalendarYear == year)
            .Select(h => new HolidayListDto(
                h.HolidayListId,
                h.Name,
                h.WorkLocationId,
                h.CalendarYear,
                h.MaxOptionalPerYear,
                h.Holidays.Select(x => new HolidayDto(x.HolidayId, x.HolidayListId, x.HolidayDate, x.Name, x.IsOptional)).ToList()))
            .ToListAsync(ct);
    }

    public async Task<HolidayListDto> CreateHolidayListAsync(CreateHolidayListRequest req, CancellationToken ct = default)
    {
        var entity = new HolidayList
        {
            Name = req.Name.Trim(),
            WorkLocationId = req.WorkLocationId,
            CalendarYear = req.CalendarYear,
            MaxOptionalPerYear = req.MaxOptionalPerYear
        };

        _db.HolidayLists.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new HolidayListDto(
            entity.HolidayListId,
            entity.Name,
            entity.WorkLocationId,
            entity.CalendarYear,
            entity.MaxOptionalPerYear,
            new List<HolidayDto>());
    }

    public async Task<HolidayDto> AddHolidayAsync(long holidayListId, CreateHolidayRequest req, CancellationToken ct = default)
    {
        var holiday = new Holiday
        {
            HolidayListId = holidayListId,
            HolidayDate = req.HolidayDate,
            Name = req.Name.Trim(),
            IsOptional = req.IsOptional
        };

        _db.Holidays.Add(holiday);
        await _db.SaveChangesAsync(ct);

        return new HolidayDto(holiday.HolidayId, holiday.HolidayListId, holiday.HolidayDate, holiday.Name, holiday.IsOptional);
    }

    public async Task AssignRosterAsync(AssignRosterRequest req, CancellationToken ct = default)
    {
        // Check overlaps
        bool overlap = await _db.ShiftRosters
            .AnyAsync(r => r.EmployeeId == req.EmployeeId && r.FromDate <= req.ToDate && r.ToDate >= req.FromDate, ct);

        if (overlap)
        {
            throw new InvalidOperationException("Roster overlaps with an existing roster assignment for this employee.");
        }

        var roster = new ShiftRoster
        {
            EmployeeId = req.EmployeeId,
            FromDate = req.FromDate,
            ToDate = req.ToDate,
            ShiftId = req.ShiftId,
            WeeklyOffPolicyId = req.WeeklyOffPolicyId
        };

        _db.ShiftRosters.Add(roster);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PunchDto> RecordPunchAsync(RecordPunchRequest req, CancellationToken ct = default)
    {
        var punch = new Punch
        {
            EmployeeId = req.EmployeeId,
            PunchedAt = req.PunchedAt,
            PunchSource = req.PunchSource,
            DeviceCode = req.DeviceCode,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            IsInsideFence = true
        };

        _db.Punches.Add(punch);
        await _db.SaveChangesAsync(ct);

        // Derive daily attendance
        var date = DateOnly.FromDateTime(req.PunchedAt.LocalDateTime);
        await DeriveDailyAttendanceAsync(req.EmployeeId, date, ct);

        return new PunchDto(
            punch.PunchId,
            punch.EmployeeId,
            punch.PunchedAt,
            punch.PunchSource,
            punch.DeviceCode,
            punch.Latitude,
            punch.Longitude,
            punch.IsInsideFence);
    }

    public async Task<int> ImportBiometricPunchesAsync(BiometricPunchImportRequest req, CancellationToken ct = default)
    {
        if (req.Punches == null || req.Punches.Count == 0)
        {
            return 0;
        }

        var deviceCode = req.DeviceCode.Trim();
        var deviceUserIds = req.Punches.Select(p => p.DeviceUserId.Trim()).Distinct().ToList();

        var mappings = await _db.BiometricDeviceUsers
            .AsNoTracking()
            .Where(u => u.DeviceCode == deviceCode && deviceUserIds.Contains(u.DeviceUserId))
            .ToDictionaryAsync(u => u.DeviceUserId, u => u.EmployeeId, ct);

        int imported = 0;
        var affectedEmployeeDates = new HashSet<(long EmployeeId, DateOnly Date)>();

        foreach (var p in req.Punches)
        {
            if (mappings.TryGetValue(p.DeviceUserId.Trim(), out long employeeId))
            {
                var punch = new Punch
                {
                    EmployeeId = employeeId,
                    PunchedAt = p.PunchedAt,
                    PunchSource = PunchSource.Biometric,
                    DeviceCode = deviceCode,
                    IsInsideFence = true
                };

                _db.Punches.Add(punch);
                imported++;

                var date = DateOnly.FromDateTime(p.PunchedAt.LocalDateTime);
                affectedEmployeeDates.Add((employeeId, date));
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var (empId, date) in affectedEmployeeDates)
        {
            await DeriveDailyAttendanceAsync(empId, date, ct);
        }

        return imported;
    }

    public async Task<DailyAttendanceDto> DeriveDailyAttendanceAsync(long employeeId, DateOnly date, CancellationToken ct = default)
    {
        var attendance = await _db.DailyAttendances
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == date, ct);

        if (attendance != null && attendance.IsLocked)
        {
            return ToDailyAttendanceDto(attendance);
        }

        // Fetch shift from roster or default
        var roster = await _db.ShiftRosters
            .AsNoTracking()
            .Include(r => r.Shift)
            .Include(r => r.WeeklyOffPolicy)
            .Where(r => r.EmployeeId == employeeId && r.FromDate <= date && r.ToDate >= date)
            .FirstOrDefaultAsync(ct);

        var shift = roster?.Shift ?? await _db.Shifts.FirstOrDefaultAsync(s => s.IsActive, ct) ?? new Shift
        {
            Code = "GEN",
            Name = "General Shift",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(18, 0),
            BreakMinutes = 60,
            GraceInMinutes = 15,
            GraceOutMinutes = 15,
            HalfDayBelowMinutes = 300,
            AbsentBelowMinutes = 180
        };

        var weeklyOff = roster?.WeeklyOffPolicy ?? await _db.WeeklyOffPolicies.FirstOrDefaultAsync(w => w.IsActive, ct) ?? new WeeklyOffPolicy
        {
            SundayRule = WeeklyOffKind.Off,
            SaturdayRule = WeeklyOffKind.AlternateOff,
            AlternateWeeks = "2,4"
        };

        var startInstant = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endInstant = new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var punches = await _db.Punches
            .AsNoTracking()
            .Where(p => p.EmployeeId == employeeId && p.PunchedAt >= startInstant && p.PunchedAt < endInstant)
            .OrderBy(p => p.PunchedAt)
            .ToListAsync(ct);

        if (attendance == null)
        {
            attendance = new DailyAttendance
            {
                EmployeeId = employeeId,
                AttendanceDate = date,
                ShiftId = shift.ShiftId
            };
            _db.DailyAttendances.Add(attendance);
        }

        attendance.ShiftId = shift.ShiftId;
        attendance.Shift = shift;

        if (punches.Count == 0)
        {
            // Check holiday
            bool isHoliday = await _db.Holidays.AnyAsync(h => h.HolidayDate == date, ct);
            if (isHoliday)
            {
                attendance.AttendanceStatus = AttendanceStatus.Holiday;
                attendance.AttendanceSource = AttendanceSource.Derived;
            }
            else if (_leaveEngine.IsNonWorkingDay(date, new HashSet<DateOnly>(), weeklyOff))
            {
                attendance.AttendanceStatus = AttendanceStatus.WeeklyOff;
                attendance.AttendanceSource = AttendanceSource.Derived;
            }
            else
            {
                // Check approved leave
                bool onLeave = await _db.LeaveApplications.AnyAsync(l => l.EmployeeId == employeeId && l.FromDate <= date && l.ToDate >= date && l.LeaveStatus == LeaveStatus.Approved, ct);
                attendance.AttendanceStatus = onLeave ? AttendanceStatus.OnLeave : AttendanceStatus.Absent;
                attendance.AttendanceSource = AttendanceSource.Derived;
            }

            attendance.FirstIn = null;
            attendance.LastOut = null;
            attendance.WorkedMinutes = 0;
            attendance.LateMinutes = 0;
            attendance.EarlyOutMinutes = 0;
            attendance.OvertimeMinutes = 0;
        }
        else
        {
            var firstIn = punches.First().PunchedAt;
            var lastOut = punches.Last().PunchedAt;

            attendance.FirstIn = firstIn;
            attendance.LastOut = lastOut;

            int spanMinutes = (int)(lastOut - firstIn).TotalMinutes;
            int workedMinutes = Math.Max(0, spanMinutes - shift.BreakMinutes);
            attendance.WorkedMinutes = workedMinutes;

            var inTime = TimeOnly.FromTimeSpan(firstIn.LocalDateTime.TimeOfDay);
            int lateMinutes = 0;
            if (inTime > shift.StartTime)
            {
                int diff = (int)(inTime.ToTimeSpan() - shift.StartTime.ToTimeSpan()).TotalMinutes;
                if (diff > shift.GraceInMinutes)
                {
                    lateMinutes = diff;
                }
            }
            attendance.LateMinutes = lateMinutes;

            var outTime = TimeOnly.FromTimeSpan(lastOut.LocalDateTime.TimeOfDay);
            int earlyOutMinutes = 0;
            if (outTime < shift.EndTime)
            {
                int diff = (int)(shift.EndTime.ToTimeSpan() - outTime.ToTimeSpan()).TotalMinutes;
                if (diff > shift.GraceOutMinutes)
                {
                    earlyOutMinutes = diff;
                }
            }
            attendance.EarlyOutMinutes = earlyOutMinutes;

            if (shift.AbsentBelowMinutes > 0 && workedMinutes < shift.AbsentBelowMinutes)
            {
                attendance.AttendanceStatus = AttendanceStatus.Absent;
            }
            else if (shift.HalfDayBelowMinutes > 0 && workedMinutes < shift.HalfDayBelowMinutes)
            {
                attendance.AttendanceStatus = AttendanceStatus.HalfDay;
            }
            else
            {
                attendance.AttendanceStatus = AttendanceStatus.Present;
            }

            attendance.AttendanceSource = AttendanceSource.Derived;
        }

        await _db.SaveChangesAsync(ct);
        return ToDailyAttendanceDto(attendance);
    }

    public async Task<RegularisationRequestDto> CreateRegularisationAsync(CreateRegularisationRequest req, CancellationToken ct = default)
    {
        var regularisation = new RegularisationRequest
        {
            EmployeeId = req.EmployeeId,
            AttendanceDate = req.AttendanceDate,
            RequestedIn = req.RequestedIn,
            RequestedOut = req.RequestedOut,
            RequestedStatus = req.RequestedStatus,
            Reason = req.Reason,
            ApprovalStatus = ApprovalStatus.InApproval,
            CurrentStepLabel = "Manager Approval"
        };

        _db.RegularisationRequests.Add(regularisation);
        await _db.SaveChangesAsync(ct);

        // Snapshot approval step
        var step = new ApprovalStep
        {
            RequestKind = RequestKind.Regularisation,
            RequestId = regularisation.RegularisationRequestId,
            Sequence = 1,
            Label = "Manager Approval",
            ApproverEmployeeId = req.ReportsToEmployeeId,
            StepStatus = ApprovalStepStatus.Pending
        };
        _db.ApprovalSteps.Add(step);
        await _db.SaveChangesAsync(ct);

        return new RegularisationRequestDto(
            regularisation.RegularisationRequestId,
            regularisation.EmployeeId,
            regularisation.AttendanceDate,
            regularisation.RequestedIn,
            regularisation.RequestedOut,
            regularisation.RequestedStatus,
            regularisation.Reason,
            regularisation.ApprovalStatus,
            regularisation.CurrentStepLabel);
    }

    public async Task ApproveRegularisationAsync(long regularisationId, Guid? userId = null, string? comments = null, CancellationToken ct = default)
    {
        var req = await _db.RegularisationRequests.FindAsync(new object[] { regularisationId }, ct);
        if (req == null) throw new KeyNotFoundException($"Regularisation request {regularisationId} not found.");

        var step = await _db.ApprovalSteps
            .FirstOrDefaultAsync(s => s.RequestKind == RequestKind.Regularisation && s.RequestId == regularisationId && s.StepStatus == ApprovalStepStatus.Pending, ct);

        if (step != null)
        {
            step.StepStatus = ApprovalStepStatus.Approved;
            step.ActedByUserId = userId;
            step.ActedAt = DateTimeOffset.UtcNow;
            step.Comments = comments;
        }

        req.ApprovalStatus = ApprovalStatus.Approved;
        req.CurrentStepLabel = "Approved";

        // Correct the daily attendance
        var attendance = await _db.DailyAttendances
            .FirstOrDefaultAsync(d => d.EmployeeId == req.EmployeeId && d.AttendanceDate == req.AttendanceDate, ct);

        if (attendance != null && !attendance.IsLocked)
        {
            attendance.AttendanceStatus = req.RequestedStatus;
            attendance.AttendanceSource = AttendanceSource.Regularised;
            if (req.RequestedIn.HasValue) attendance.FirstIn = req.RequestedIn;
            if (req.RequestedOut.HasValue) attendance.LastOut = req.RequestedOut;
            attendance.LateMinutes = 0;
            attendance.EarlyOutMinutes = 0;
            if (req.RequestedIn.HasValue && req.RequestedOut.HasValue)
            {
                int span = (int)(req.RequestedOut.Value - req.RequestedIn.Value).TotalMinutes;
                attendance.WorkedMinutes = Math.Max(0, span - 60);
            }
        }
        else if (attendance == null)
        {
            _db.DailyAttendances.Add(new DailyAttendance
            {
                EmployeeId = req.EmployeeId,
                AttendanceDate = req.AttendanceDate,
                AttendanceStatus = req.RequestedStatus,
                AttendanceSource = AttendanceSource.Regularised,
                FirstIn = req.RequestedIn,
                LastOut = req.RequestedOut,
                LateMinutes = 0,
                EarlyOutMinutes = 0,
                WorkedMinutes = req.RequestedIn.HasValue && req.RequestedOut.HasValue ? Math.Max(0, (int)(req.RequestedOut.Value - req.RequestedIn.Value).TotalMinutes - 60) : 480
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<OvertimeRequestDto> CreateOvertimeAsync(CreateOvertimeRequest req, CancellationToken ct = default)
    {
        var overtime = new OvertimeRequest
        {
            EmployeeId = req.EmployeeId,
            AttendanceDate = req.AttendanceDate,
            Minutes = req.Minutes,
            OvertimeRate = req.OvertimeRate,
            ApprovalStatus = ApprovalStatus.InApproval,
            CurrentStepLabel = "Manager Approval"
        };

        _db.OvertimeRequests.Add(overtime);
        await _db.SaveChangesAsync(ct);

        var otStep = new ApprovalStep
        {
            RequestKind = RequestKind.Overtime,
            RequestId = overtime.OvertimeRequestId,
            Sequence = 1,
            Label = "Manager Approval",
            ApproverEmployeeId = req.ReportsToEmployeeId,
            StepStatus = ApprovalStepStatus.Pending
        };
        _db.ApprovalSteps.Add(otStep);
        await _db.SaveChangesAsync(ct);

        return new OvertimeRequestDto(
            overtime.OvertimeRequestId,
            overtime.EmployeeId,
            overtime.AttendanceDate,
            overtime.Minutes,
            overtime.OvertimeRate,
            overtime.ApprovalStatus,
            overtime.CurrentStepLabel);
    }

    public async Task ApproveOvertimeAsync(long overtimeId, Guid? userId = null, CancellationToken ct = default)
    {
        var req = await _db.OvertimeRequests.FindAsync(new object[] { overtimeId }, ct);
        if (req == null) throw new KeyNotFoundException($"Overtime request {overtimeId} not found.");

        req.ApprovalStatus = ApprovalStatus.Approved;
        req.CurrentStepLabel = "Approved";

        var attendance = await _db.DailyAttendances
            .FirstOrDefaultAsync(d => d.EmployeeId == req.EmployeeId && d.AttendanceDate == req.AttendanceDate, ct);

        if (attendance != null && !attendance.IsLocked)
        {
            attendance.OvertimeMinutes += req.Minutes;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<MonthlyPaidDaysDto> GetMonthlyAttendanceSummaryAsync(long employeeId, int year, int month, CancellationToken ct = default)
    {
        var start = new DateOnly(year, month, 1);
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var end = new DateOnly(year, month, daysInMonth);

        var attendances = await _db.DailyAttendances
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.AttendanceDate >= start && a.AttendanceDate <= end)
            .ToListAsync(ct);

        decimal present = attendances.Count(a => a.AttendanceStatus == AttendanceStatus.Present || a.AttendanceStatus == AttendanceStatus.OnDuty);
        decimal halfDays = attendances.Count(a => a.AttendanceStatus == AttendanceStatus.HalfDay);
        decimal absent = attendances.Count(a => a.AttendanceStatus == AttendanceStatus.Absent);
        decimal holidays = attendances.Count(a => a.AttendanceStatus == AttendanceStatus.Holiday);
        decimal weeklyOffs = attendances.Count(a => a.AttendanceStatus == AttendanceStatus.WeeklyOff);

        // Fetch leaves in month
        var leaveApps = await _db.LeaveApplications
            .AsNoTracking()
            .Include(l => l.LeaveType)
            .Where(l => l.EmployeeId == employeeId && l.FromDate <= end && l.ToDate >= start && l.LeaveStatus == LeaveStatus.Approved)
            .ToListAsync(ct);

        decimal paidLeaves = leaveApps.Where(l => l.LeaveType != null && l.LeaveType.IsPaid).Sum(l => l.Days);
        decimal unpaidLeaves = leaveApps.Where(l => l.LeaveType != null && !l.LeaveType.IsPaid).Sum(l => l.Days);

        decimal totalPaidDays = present + (halfDays * 0.5m) + holidays + weeklyOffs + paidLeaves;

        return new MonthlyPaidDaysDto(
            employeeId,
            year,
            month,
            daysInMonth,
            present,
            halfDays,
            absent,
            paidLeaves,
            unpaidLeaves,
            holidays,
            weeklyOffs,
            totalPaidDays);
    }

    public async Task<List<DailyAttendanceDto>> GetAttendanceRangeAsync(long employeeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        return await _db.DailyAttendances
            .AsNoTracking()
            .Include(a => a.Shift)
            .Where(a => a.EmployeeId == employeeId && a.AttendanceDate >= from && a.AttendanceDate <= to)
            .OrderBy(a => a.AttendanceDate)
            .Select(a => ToDailyAttendanceDto(a))
            .ToListAsync(ct);
    }

    private static DailyAttendanceDto ToDailyAttendanceDto(DailyAttendance a)
    {
        return new DailyAttendanceDto(
            a.DailyAttendanceId,
            a.EmployeeId,
            a.AttendanceDate,
            a.ShiftId,
            a.Shift?.Name,
            a.FirstIn,
            a.LastOut,
            a.WorkedMinutes,
            a.LateMinutes,
            a.EarlyOutMinutes,
            a.OvertimeMinutes,
            a.AttendanceStatus,
            a.AttendanceSource,
            a.IsLocked);
    }

    public async Task<List<PunchDto>> GetPunchesForDateAsync(long employeeId, DateOnly date, CancellationToken ct = default)
    {
        var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        return await _db.Punches
            .AsNoTracking()
            .Where(p => p.EmployeeId == employeeId && p.PunchedAt >= start && p.PunchedAt <= end)
            .OrderBy(p => p.PunchedAt)
            .Select(p => new PunchDto(p.PunchId, p.EmployeeId, p.PunchedAt, p.PunchSource, p.DeviceCode, p.Latitude, p.Longitude, p.IsInsideFence))
            .ToListAsync(ct);
    }

    public async Task<List<RegularisationRequest>> GetRegularisationRequestsAsync(long employeeId, CancellationToken ct = default)
    {
        return await _db.RegularisationRequests
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.AttendanceDate)
            .ToListAsync(ct);
    }

    public async Task<List<OvertimeRequest>> GetOvertimeRequestsAsync(long employeeId, CancellationToken ct = default)
    {
        return await _db.OvertimeRequests
            .AsNoTracking()
            .Where(o => o.EmployeeId == employeeId)
            .OrderByDescending(o => o.AttendanceDate)
            .ToListAsync(ct);
    }
}


using Microsoft.EntityFrameworkCore;
using TimeLeave.Entity.Enums;
using TimeLeave.Entity.Models;
using TimeLeave.Entity.TableEntities;
using TimeLeave.Repository;

namespace TimeLeave.Api.Services;

public class LeaveService
{
    private readonly TimeLeaveDbContext _db;
    private readonly LeaveCalculationEngine _engine;

    public LeaveService(TimeLeaveDbContext db, LeaveCalculationEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public async Task<List<LeaveTypeDto>> GetLeaveTypesAsync(CancellationToken ct = default)
    {
        return await _db.LeaveTypes
            .AsNoTracking()
            .Select(t => new LeaveTypeDto(
                t.LeaveTypeId,
                t.Code,
                t.Name,
                t.IsPaid,
                t.IsHalfDayAllowed,
                t.IsAttachmentRequiredAboveDays,
                t.Gender,
                t.IsActive))
            .ToListAsync(ct);
    }

    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeRequest req, CancellationToken ct = default)
    {
        var entity = new LeaveType
        {
            Code = req.Code.Trim().ToUpperInvariant(),
            Name = req.Name.Trim(),
            IsPaid = req.IsPaid,
            IsHalfDayAllowed = req.IsHalfDayAllowed,
            IsAttachmentRequiredAboveDays = req.IsAttachmentRequiredAboveDays,
            Gender = req.Gender,
            IsActive = true
        };

        _db.LeaveTypes.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new LeaveTypeDto(
            entity.LeaveTypeId,
            entity.Code,
            entity.Name,
            entity.IsPaid,
            entity.IsHalfDayAllowed,
            entity.IsAttachmentRequiredAboveDays,
            entity.Gender,
            entity.IsActive);
    }

    public async Task<List<LeavePolicyDto>> GetLeavePoliciesAsync(CancellationToken ct = default)
    {
        return await _db.LeavePolicies
            .AsNoTracking()
            .Include(p => p.LeaveType)
            .Select(p => new LeavePolicyDto(
                p.LeavePolicyId,
                p.LeaveTypeId,
                p.LeaveType != null ? p.LeaveType.Name : null,
                p.GradeId,
                p.WorkLocationId,
                p.EffectiveFrom,
                p.AnnualQuota,
                p.AccrualKind,
                p.IsProratedOnJoining,
                p.CarryForwardKind,
                p.MaxCarryForward,
                p.MaxEncashPerYear,
                p.MinDaysPerApplication,
                p.MaxDaysPerApplication,
                p.NoticeDays,
                p.IsSandwichRule,
                p.CanApplyInProbation))
            .ToListAsync(ct);
    }

    public async Task<LeavePolicyDto> CreateLeavePolicyAsync(CreateLeavePolicyRequest req, CancellationToken ct = default)
    {
        var entity = new LeavePolicy
        {
            LeaveTypeId = req.LeaveTypeId,
            GradeId = req.GradeId,
            WorkLocationId = req.WorkLocationId,
            EffectiveFrom = req.EffectiveFrom,
            AnnualQuota = req.AnnualQuota,
            AccrualKind = req.AccrualKind,
            IsProratedOnJoining = req.IsProratedOnJoining,
            CarryForwardKind = req.CarryForwardKind,
            MaxCarryForward = req.MaxCarryForward,
            MaxEncashPerYear = req.MaxEncashPerYear,
            MinDaysPerApplication = req.MinDaysPerApplication,
            MaxDaysPerApplication = req.MaxDaysPerApplication,
            NoticeDays = req.NoticeDays,
            IsSandwichRule = req.IsSandwichRule,
            CanApplyInProbation = req.CanApplyInProbation
        };

        _db.LeavePolicies.Add(entity);
        await _db.SaveChangesAsync(ct);

        var leaveType = await _db.LeaveTypes.FindAsync(new object[] { req.LeaveTypeId }, ct);

        return new LeavePolicyDto(
            entity.LeavePolicyId,
            entity.LeaveTypeId,
            leaveType?.Name,
            entity.GradeId,
            entity.WorkLocationId,
            entity.EffectiveFrom,
            entity.AnnualQuota,
            entity.AccrualKind,
            entity.IsProratedOnJoining,
            entity.CarryForwardKind,
            entity.MaxCarryForward,
            entity.MaxEncashPerYear,
            entity.MinDaysPerApplication,
            entity.MaxDaysPerApplication,
            entity.NoticeDays,
            entity.IsSandwichRule,
            entity.CanApplyInProbation);
    }

    public async Task<List<LeaveBalanceDto>> GetBalancesAsync(long employeeId, int year, CancellationToken ct = default)
    {
        return await _db.LeaveBalances
            .AsNoTracking()
            .Include(b => b.LeaveType)
            .Where(b => b.EmployeeId == employeeId && b.LeaveYear == year)
            .Select(b => new LeaveBalanceDto(
                b.LeaveBalanceId,
                b.EmployeeId,
                b.LeaveTypeId,
                b.LeaveType != null ? b.LeaveType.Code : string.Empty,
                b.LeaveType != null ? b.LeaveType.Name : string.Empty,
                b.LeaveYear,
                b.Opening,
                b.Accrued,
                b.Taken,
                b.Encashed,
                b.Lapsed,
                b.Adjusted,
                (b.Opening + b.Accrued + b.Adjusted) - (b.Taken + b.Encashed + b.Lapsed)))
            .ToListAsync(ct);
    }

    public async Task<LeaveApplicationDto> ApplyLeaveAsync(ApplyLeaveRequest req, Guid? userId = null, CancellationToken ct = default)
    {
        if (req.ToDate < req.FromDate)
        {
            throw new ArgumentException("ToDate must be greater than or equal to FromDate.");
        }

        var policy = await _db.LeavePolicies
            .AsNoTracking()
            .Where(p => p.LeaveTypeId == req.LeaveTypeId && p.EffectiveFrom <= req.FromDate)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(ct) ?? new LeavePolicy
            {
                LeaveTypeId = req.LeaveTypeId,
                IsSandwichRule = false,
                AnnualQuota = 12m
            };

        // Gather holidays in range
        var holidays = await _db.Holidays
            .AsNoTracking()
            .Where(h => h.HolidayDate >= req.FromDate && h.HolidayDate <= req.ToDate)
            .Select(h => h.HolidayDate)
            .ToListAsync(ct);

        // Fetch roster or default weekly off
        var roster = await _db.ShiftRosters
            .AsNoTracking()
            .Include(r => r.WeeklyOffPolicy)
            .Where(r => r.EmployeeId == req.EmployeeId && r.FromDate <= req.ToDate && r.ToDate >= req.FromDate)
            .FirstOrDefaultAsync(ct);

        var weeklyOff = roster?.WeeklyOffPolicy ?? await _db.WeeklyOffPolicies.FirstOrDefaultAsync(w => w.IsActive, ct) ?? new WeeklyOffPolicy
        {
            Name = "Default Weekly Off",
            SundayRule = WeeklyOffKind.Off,
            SaturdayRule = WeeklyOffKind.AlternateOff,
            AlternateWeeks = "2,4"
        };

        decimal days = _engine.CalculateDays(
            req.FromDate,
            req.ToDate,
            req.FromHalf,
            req.ToHalf,
            policy,
            new HashSet<DateOnly>(holidays),
            weeklyOff);

        if (days <= 0m)
        {
            throw new InvalidOperationException("Computed leave duration is 0 days.");
        }

        // Check policy min/max days
        if (policy.MinDaysPerApplication.HasValue && days < policy.MinDaysPerApplication.Value)
        {
            throw new InvalidOperationException($"Minimum days per application is {policy.MinDaysPerApplication.Value}.");
        }
        if (policy.MaxDaysPerApplication.HasValue && days > policy.MaxDaysPerApplication.Value)
        {
            throw new InvalidOperationException($"Maximum days per application is {policy.MaxDaysPerApplication.Value}.");
        }

        // Check balance
        int year = req.FromDate.Year;
        var balance = await _db.LeaveBalances
            .FirstOrDefaultAsync(b => b.EmployeeId == req.EmployeeId && b.LeaveTypeId == req.LeaveTypeId && b.LeaveYear == year, ct);

        decimal available = balance == null
            ? 0m
            : (balance.Opening + balance.Accrued + balance.Adjusted) - (balance.Taken + balance.Encashed + balance.Lapsed);

        if (available < days)
        {
            throw new InvalidOperationException($"Insufficient leave balance. Requested: {days}, Available: {available}.");
        }

        var app = new LeaveApplication
        {
            EmployeeId = req.EmployeeId,
            LeaveTypeId = req.LeaveTypeId,
            FromDate = req.FromDate,
            ToDate = req.ToDate,
            FromHalf = req.FromHalf,
            ToHalf = req.ToHalf,
            Days = days,
            Reason = req.Reason,
            AttachmentKey = req.AttachmentKey,
            LeaveStatus = LeaveStatus.Submitted,
            ApprovalStatus = ApprovalStatus.InApproval,
            CurrentStepLabel = "Manager Approval",
            CurrentApproverEmployeeId = req.ReportsToEmployeeId
        };

        _db.LeaveApplications.Add(app);
        await _db.SaveChangesAsync(ct);

        // Snapshot approval workflow levels into ApprovalStep rows
        var step1 = new ApprovalStep
        {
            RequestKind = RequestKind.Leave,
            RequestId = app.LeaveApplicationId,
            Sequence = 1,
            Label = "Manager Approval",
            ApproverEmployeeId = req.ReportsToEmployeeId,
            StepStatus = ApprovalStepStatus.Pending,
            DueDate = req.FromDate
        };

        var step2 = new ApprovalStep
        {
            RequestKind = RequestKind.Leave,
            RequestId = app.LeaveApplicationId,
            Sequence = 2,
            Label = "HR Approval",
            StepStatus = ApprovalStepStatus.Waiting,
            DueDate = req.FromDate
        };

        _db.ApprovalSteps.AddRange(step1, step2);
        await _db.SaveChangesAsync(ct);

        var leaveType = await _db.LeaveTypes.FindAsync(new object[] { req.LeaveTypeId }, ct);

        return new LeaveApplicationDto(
            app.LeaveApplicationId,
            app.EmployeeId,
            app.LeaveTypeId,
            leaveType?.Code ?? "",
            leaveType?.Name ?? "",
            app.FromDate,
            app.ToDate,
            app.FromHalf,
            app.ToHalf,
            app.Days,
            app.Reason,
            app.AttachmentKey,
            app.LeaveStatus,
            app.ApprovalStatus,
            app.CurrentStepLabel,
            app.CurrentApproverEmployeeId);
    }

    public async Task<LeaveApplicationDto> ApproveLeaveAsync(long applicationId, Guid? userId = null, string? comments = null, CancellationToken ct = default)
    {
        var app = await _db.LeaveApplications
            .Include(a => a.LeaveType)
            .FirstOrDefaultAsync(a => a.LeaveApplicationId == applicationId, ct);

        if (app == null)
        {
            throw new KeyNotFoundException($"Leave application {applicationId} not found.");
        }

        if (app.LeaveStatus == LeaveStatus.Approved)
        {
            throw new InvalidOperationException("Leave application is already approved.");
        }

        // Progress approval step
        var steps = await _db.ApprovalSteps
            .Where(s => s.RequestKind == RequestKind.Leave && s.RequestId == applicationId)
            .OrderBy(s => s.Sequence)
            .ToListAsync(ct);

        var currentStep = steps.FirstOrDefault(s => s.StepStatus == ApprovalStepStatus.Pending);
        if (currentStep != null)
        {
            currentStep.StepStatus = ApprovalStepStatus.Approved;
            currentStep.ActedByUserId = userId;
            currentStep.ActedAt = DateTimeOffset.UtcNow;
            currentStep.Comments = comments;

            var nextStep = steps.FirstOrDefault(s => s.Sequence > currentStep.Sequence && s.StepStatus == ApprovalStepStatus.Waiting);
            if (nextStep != null)
            {
                nextStep.StepStatus = ApprovalStepStatus.Pending;
                app.CurrentStepLabel = nextStep.Label;
                await _db.SaveChangesAsync(ct);

                return new LeaveApplicationDto(
                    app.LeaveApplicationId,
                    app.EmployeeId,
                    app.LeaveTypeId,
                    app.LeaveType?.Code ?? "",
                    app.LeaveType?.Name ?? "",
                    app.FromDate,
                    app.ToDate,
                    app.FromHalf,
                    app.ToHalf,
                    app.Days,
                    app.Reason,
                    app.AttachmentKey,
                    app.LeaveStatus,
                    app.ApprovalStatus,
                    app.CurrentStepLabel,
                    app.CurrentApproverEmployeeId);
            }
        }

        // All steps approved -> Final Approval with balance guard
        int year = app.FromDate.Year;
        int updated = await _db.LeaveBalances
            .Where(b => b.EmployeeId == app.EmployeeId
                        && b.LeaveTypeId == app.LeaveTypeId
                        && b.LeaveYear == year
                        && (b.Opening + b.Accrued + b.Adjusted - b.Taken - b.Encashed - b.Lapsed) >= app.Days)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Taken, b => b.Taken + app.Days), ct);

        if (updated == 0)
        {
            throw new InvalidOperationException("Insufficient leave balance to approve application.");
        }

        app.LeaveStatus = LeaveStatus.Approved;
        app.ApprovalStatus = ApprovalStatus.Approved;
        app.CurrentStepLabel = "Approved";

        // Mark daily attendance for the leave dates
        for (var date = app.FromDate; date <= app.ToDate; date = date.AddDays(1))
        {
            var attendance = await _db.DailyAttendances
                .FirstOrDefaultAsync(d => d.EmployeeId == app.EmployeeId && d.AttendanceDate == date, ct);

            if (attendance != null && !attendance.IsLocked)
            {
                attendance.AttendanceStatus = AttendanceStatus.OnLeave;
                attendance.AttendanceSource = AttendanceSource.Manual;
            }
            else if (attendance == null)
            {
                _db.DailyAttendances.Add(new DailyAttendance
                {
                    EmployeeId = app.EmployeeId,
                    AttendanceDate = date,
                    AttendanceStatus = AttendanceStatus.OnLeave,
                    AttendanceSource = AttendanceSource.Manual
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        return new LeaveApplicationDto(
            app.LeaveApplicationId,
            app.EmployeeId,
            app.LeaveTypeId,
            app.LeaveType?.Code ?? "",
            app.LeaveType?.Name ?? "",
            app.FromDate,
            app.ToDate,
            app.FromHalf,
            app.ToHalf,
            app.Days,
            app.Reason,
            app.AttachmentKey,
            app.LeaveStatus,
            app.ApprovalStatus,
            app.CurrentStepLabel,
            app.CurrentApproverEmployeeId);
    }

    public async Task RejectLeaveAsync(long applicationId, Guid? userId = null, string? reason = null, CancellationToken ct = default)
    {
        var app = await _db.LeaveApplications.FindAsync(new object[] { applicationId }, ct);
        if (app == null) throw new KeyNotFoundException($"Leave application {applicationId} not found.");

        var currentStep = await _db.ApprovalSteps
            .FirstOrDefaultAsync(s => s.RequestKind == RequestKind.Leave && s.RequestId == applicationId && s.StepStatus == ApprovalStepStatus.Pending, ct);

        if (currentStep != null)
        {
            currentStep.StepStatus = ApprovalStepStatus.Rejected;
            currentStep.ActedByUserId = userId;
            currentStep.ActedAt = DateTimeOffset.UtcNow;
            currentStep.Comments = reason;
        }

        app.LeaveStatus = LeaveStatus.Rejected;
        app.ApprovalStatus = ApprovalStatus.Rejected;
        app.CurrentStepLabel = "Rejected";

        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelLeaveAsync(long applicationId, CancellationToken ct = default)
    {
        var app = await _db.LeaveApplications.FindAsync(new object[] { applicationId }, ct);
        if (app == null) throw new KeyNotFoundException($"Leave application {applicationId} not found.");

        if (app.LeaveStatus == LeaveStatus.Approved)
        {
            // Restore balance
            int year = app.FromDate.Year;
            await _db.LeaveBalances
                .Where(b => b.EmployeeId == app.EmployeeId && b.LeaveTypeId == app.LeaveTypeId && b.LeaveYear == year)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Taken, b => b.Taken - app.Days), ct);
        }

        app.LeaveStatus = LeaveStatus.Cancelled;
        app.CurrentStepLabel = "Cancelled";

        await _db.SaveChangesAsync(ct);
    }

    public async Task AccrueLeaveAsync(long employeeId, long leaveTypeId, int year, decimal amount, CancellationToken ct = default)
    {
        var balance = await _db.LeaveBalances
            .FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.LeaveYear == year, ct);

        if (balance == null)
        {
            balance = new LeaveBalance
            {
                EmployeeId = employeeId,
                LeaveTypeId = leaveTypeId,
                LeaveYear = year,
                Accrued = amount
            };
            _db.LeaveBalances.Add(balance);
        }
        else
        {
            balance.Accrued += amount;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<LeaveEncashmentDto> ApplyEncashmentAsync(ApplyEncashmentRequest req, CancellationToken ct = default)
    {
        var balance = await _db.LeaveBalances
            .FirstOrDefaultAsync(b => b.EmployeeId == req.EmployeeId && b.LeaveTypeId == req.LeaveTypeId && b.LeaveYear == req.LeaveYear, ct);

        decimal available = balance == null
            ? 0m
            : (balance.Opening + balance.Accrued + balance.Adjusted) - (balance.Taken + balance.Encashed + balance.Lapsed);

        if (available < req.Days)
        {
            throw new InvalidOperationException($"Insufficient leave balance to encash. Requested: {req.Days}, Available: {available}.");
        }

        var encashment = new LeaveEncashment
        {
            EmployeeId = req.EmployeeId,
            LeaveTypeId = req.LeaveTypeId,
            LeaveYear = req.LeaveYear,
            Days = req.Days,
            EncashmentStatus = EncashmentStatus.Submitted,
            ApprovalStatus = ApprovalStatus.InApproval,
            CurrentStepLabel = "Manager Approval"
        };

        _db.LeaveEncashments.Add(encashment);
        await _db.SaveChangesAsync(ct);

        var leaveType = await _db.LeaveTypes.FindAsync(new object[] { req.LeaveTypeId }, ct);

        return new LeaveEncashmentDto(
            encashment.LeaveEncashmentId,
            encashment.EmployeeId,
            encashment.LeaveTypeId,
            leaveType?.Code ?? "",
            leaveType?.Name ?? "",
            encashment.LeaveYear,
            encashment.Days,
            encashment.EncashmentStatus,
            encashment.ApprovalStatus,
            encashment.CurrentStepLabel);
    }

    public async Task<LeaveEncashmentDto> ApproveEncashmentAsync(long encashmentId, Guid? userId = null, CancellationToken ct = default)
    {
        var encashment = await _db.LeaveEncashments
            .Include(e => e.LeaveType)
            .FirstOrDefaultAsync(e => e.LeaveEncashmentId == encashmentId, ct);

        if (encashment == null) throw new KeyNotFoundException($"Leave encashment {encashmentId} not found.");

        int updated = await _db.LeaveBalances
            .Where(b => b.EmployeeId == encashment.EmployeeId
                        && b.LeaveTypeId == encashment.LeaveTypeId
                        && b.LeaveYear == encashment.LeaveYear
                        && (b.Opening + b.Accrued + b.Adjusted - b.Taken - b.Encashed - b.Lapsed) >= encashment.Days)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Encashed, b => b.Encashed + encashment.Days), ct);

        if (updated == 0)
        {
            throw new InvalidOperationException("Insufficient leave balance to encash.");
        }

        encashment.EncashmentStatus = EncashmentStatus.Approved;
        encashment.ApprovalStatus = ApprovalStatus.Approved;
        encashment.CurrentStepLabel = "Approved";

        await _db.SaveChangesAsync(ct);

        return new LeaveEncashmentDto(
            encashment.LeaveEncashmentId,
            encashment.EmployeeId,
            encashment.LeaveTypeId,
            encashment.LeaveType?.Code ?? "",
            encashment.LeaveType?.Name ?? "",
            encashment.LeaveYear,
            encashment.Days,
            encashment.EncashmentStatus,
            encashment.ApprovalStatus,
            encashment.CurrentStepLabel);
    }
}

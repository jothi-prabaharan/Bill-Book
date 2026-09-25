using Microsoft.EntityFrameworkCore;
using TimeLeave.Entity.Enums;
using TimeLeave.Entity.TableEntities;
using TimeLeave.Repository;

namespace TimeLeave.Api.Services;

public class TimeLeaveSeeder
{
    private readonly TimeLeaveDbContext _db;
    private readonly ILogger<TimeLeaveSeeder> _logger;

    public TimeLeaveSeeder(TimeLeaveDbContext db, ILogger<TimeLeaveSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedBranchAsync(Guid orgId, CancellationToken ct = default)
    {
        _logger.LogInformation("Seeding default TimeLeave data for organization {OrgId}...", orgId);

        // 1. Leave Types
        if (!await _db.LeaveTypes.IgnoreQueryFilters().AnyAsync(t => t.OrgId == orgId, ct))
        {
            var cl = new LeaveType { OrgId = orgId, Code = "CL", Name = "Casual Leave", IsPaid = true, IsHalfDayAllowed = true, IsActive = true };
            var sl = new LeaveType { OrgId = orgId, Code = "SL", Name = "Sick Leave", IsPaid = true, IsHalfDayAllowed = true, IsAttachmentRequiredAboveDays = 2m, IsActive = true };
            var el = new LeaveType { OrgId = orgId, Code = "EL", Name = "Earned Leave", IsPaid = true, IsHalfDayAllowed = false, IsActive = true };
            var lop = new LeaveType { OrgId = orgId, Code = "LOP", Name = "Loss of Pay", IsPaid = false, IsHalfDayAllowed = true, IsActive = true };
            var coff = new LeaveType { OrgId = orgId, Code = "COFF", Name = "Compensatory Off", IsPaid = true, IsHalfDayAllowed = true, IsActive = true };

            _db.LeaveTypes.AddRange(cl, sl, el, lop, coff);
            await _db.SaveChangesAsync(ct);

            // Default policies
            var clPolicy = new LeavePolicy
            {
                OrgId = orgId,
                LeaveTypeId = cl.LeaveTypeId,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                AnnualQuota = 12m,
                AccrualKind = AccrualKind.Upfront,
                IsProratedOnJoining = true,
                CarryForwardKind = CarryForwardKind.Lapse,
                IsSandwichRule = false,
                CanApplyInProbation = true
            };

            var elPolicy = new LeavePolicy
            {
                OrgId = orgId,
                LeaveTypeId = el.LeaveTypeId,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                AnnualQuota = 15m,
                AccrualKind = AccrualKind.Monthly,
                IsProratedOnJoining = true,
                CarryForwardKind = CarryForwardKind.CarryForward,
                MaxCarryForward = 30m,
                MaxEncashPerYear = 10m,
                IsSandwichRule = true,
                CanApplyInProbation = false
            };

            _db.LeavePolicies.AddRange(clPolicy, elPolicy);
            await _db.SaveChangesAsync(ct);
        }

        // 2. Default Shift
        if (!await _db.Shifts.IgnoreQueryFilters().AnyAsync(s => s.OrgId == orgId, ct))
        {
            var shift = new Shift
            {
                OrgId = orgId,
                Code = "GEN",
                Name = "General Shift",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(18, 0),
                BreakMinutes = 60,
                GraceInMinutes = 15,
                GraceOutMinutes = 15,
                HalfDayBelowMinutes = 300,
                AbsentBelowMinutes = 180,
                IsNightShift = false,
                IsActive = true
            };
            _db.Shifts.Add(shift);
            await _db.SaveChangesAsync(ct);
        }

        // 3. Default Weekly Off Policy
        if (!await _db.WeeklyOffPolicies.IgnoreQueryFilters().AnyAsync(w => w.OrgId == orgId, ct))
        {
            var weeklyOff = new WeeklyOffPolicy
            {
                OrgId = orgId,
                Name = "Standard General Weekly Off",
                MondayRule = WeeklyOffKind.Working,
                TuesdayRule = WeeklyOffKind.Working,
                WednesdayRule = WeeklyOffKind.Working,
                ThursdayRule = WeeklyOffKind.Working,
                FridayRule = WeeklyOffKind.Working,
                SaturdayRule = WeeklyOffKind.AlternateOff,
                SundayRule = WeeklyOffKind.Off,
                AlternateWeeks = "2,4",
                IsActive = true
            };
            _db.WeeklyOffPolicies.Add(weeklyOff);
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Default TimeLeave data seeded successfully for {OrgId}.", orgId);
    }
}

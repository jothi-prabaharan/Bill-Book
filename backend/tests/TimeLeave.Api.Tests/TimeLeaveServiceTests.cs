using Microsoft.EntityFrameworkCore;
using TimeLeave.Api.Services;
using TimeLeave.Entity.Enums;
using TimeLeave.Entity.Models;
using TimeLeave.Entity.TableEntities;
using TimeLeave.Repository;
using Xunit;

namespace TimeLeave.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class TimeLeaveServiceTests
{
    private readonly PostgresFixture _postgres;

    public TimeLeaveServiceTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public void The_sandwich_rule_counts_a_weekend_between_two_leave_days()
    {
        var engine = new LeaveCalculationEngine();

        // 2026-10-02 is a Friday, 2026-10-05 is a Monday
        var fromDate = new DateOnly(2026, 10, 2);
        var toDate = new DateOnly(2026, 10, 5);

        var weeklyOff = new WeeklyOffPolicy
        {
            Name = "Standard Off",
            SaturdayRule = WeeklyOffKind.Off,
            SundayRule = WeeklyOffKind.Off
        };

        var sandwichPolicy = new LeavePolicy
        {
            IsSandwichRule = true,
            AnnualQuota = 15m
        };

        var nonSandwichPolicy = new LeavePolicy
        {
            IsSandwichRule = false,
            AnnualQuota = 15m
        };

        var holidays = new HashSet<DateOnly>();

        // Sandwich rule active: Friday, Saturday, Sunday, Monday = 4 days
        decimal sandwichDays = engine.CalculateDays(
            fromDate,
            toDate,
            LeaveHalf.Full,
            LeaveHalf.Full,
            sandwichPolicy,
            holidays,
            weeklyOff);

        Assert.Equal(4.0m, sandwichDays);

        // Non-sandwich rule: Friday + Monday = 2 days
        decimal nonSandwichDays = engine.CalculateDays(
            fromDate,
            toDate,
            LeaveHalf.Full,
            LeaveHalf.Full,
            nonSandwichPolicy,
            holidays,
            weeklyOff);

        Assert.Equal(2.0m, nonSandwichDays);
    }

    [SkippableFact]
    public async Task Two_simultaneous_approvals_cannot_overspend_a_balance()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using TimeLeaveDbContext db = _postgres.CreateContext(customerId, orgId);

        var leaveType = new LeaveType
        {
            OrgId = orgId,
            Code = "EL",
            Name = "Earned Leave",
            IsPaid = true,
            IsActive = true
        };
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();

        long employeeId = 1001;
        int year = 2026;

        // Seed a balance with exactly 2 days available
        var balance = new LeaveBalance
        {
            OrgId = orgId,
            EmployeeId = employeeId,
            LeaveTypeId = leaveType.LeaveTypeId,
            LeaveYear = year,
            Opening = 2m,
            Accrued = 0m,
            Taken = 0m
        };
        db.LeaveBalances.Add(balance);

        // Two leave applications of 2 days each
        var app1 = new LeaveApplication
        {
            OrgId = orgId,
            EmployeeId = employeeId,
            LeaveTypeId = leaveType.LeaveTypeId,
            FromDate = new DateOnly(2026, 6, 1),
            ToDate = new DateOnly(2026, 6, 2),
            Days = 2m,
            Reason = "Vacation A",
            LeaveStatus = LeaveStatus.Submitted,
            ApprovalStatus = ApprovalStatus.InApproval
        };

        var app2 = new LeaveApplication
        {
            OrgId = orgId,
            EmployeeId = employeeId,
            LeaveTypeId = leaveType.LeaveTypeId,
            FromDate = new DateOnly(2026, 6, 8),
            ToDate = new DateOnly(2026, 6, 9),
            Days = 2m,
            Reason = "Vacation B",
            LeaveStatus = LeaveStatus.Submitted,
            ApprovalStatus = ApprovalStatus.InApproval
        };

        db.LeaveApplications.AddRange(app1, app2);
        await db.SaveChangesAsync();

        var engine = new LeaveCalculationEngine();
        var service1 = new LeaveService(db, engine);

        // First approval succeeds
        var approved1 = await service1.ApproveLeaveAsync(app1.LeaveApplicationId);
        Assert.Equal(LeaveStatus.Approved, approved1.LeaveStatus);

        // Second approval must fail because balance is exhausted (0 left, 2 needed)
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service1.ApproveLeaveAsync(app2.LeaveApplicationId);
        });

        // Verify balance in DB: Taken must be exactly 2m, not 4m
        var finalBalance = await db.LeaveBalances.AsNoTracking()
            .FirstAsync(b => b.LeaveBalanceId == balance.LeaveBalanceId);

        Assert.Equal(2m, finalBalance.Taken);
        Assert.Equal(0m, (finalBalance.Opening + finalBalance.Accrued) - finalBalance.Taken);
    }

    [SkippableFact]
    public async Task Changing_a_workflow_leaves_requests_already_in_flight_on_their_old_chain()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using TimeLeaveDbContext db = _postgres.CreateContext(customerId, orgId);

        var leaveType = new LeaveType { OrgId = orgId, Code = "CL", Name = "Casual", IsPaid = true, IsActive = true };
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();

        long employeeId = 2002;
        db.LeaveBalances.Add(new LeaveBalance
        {
            OrgId = orgId,
            EmployeeId = employeeId,
            LeaveTypeId = leaveType.LeaveTypeId,
            LeaveYear = 2026,
            Opening = 10m
        });
        await db.SaveChangesAsync();

        var engine = new LeaveCalculationEngine();
        var service = new LeaveService(db, engine);

        var appDto = await service.ApplyLeaveAsync(new ApplyLeaveRequest(
            employeeId,
            leaveType.LeaveTypeId,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1),
            LeaveHalf.Full,
            LeaveHalf.Full,
            "Personal",
            null));

        // In-flight steps snapshotted
        var inFlightSteps = await db.ApprovalSteps
            .Where(s => s.RequestKind == RequestKind.Leave && s.RequestId == appDto.LeaveApplicationId)
            .OrderBy(s => s.Sequence)
            .ToListAsync();

        Assert.Equal(2, inFlightSteps.Count);
        Assert.Equal("Manager Approval", inFlightSteps[0].Label);
        Assert.Equal("HR Approval", inFlightSteps[1].Label);

        // Advance step 1
        var approvedStep1 = await service.ApproveLeaveAsync(appDto.LeaveApplicationId, Guid.NewGuid(), "Approved by Manager");
        Assert.Equal("HR Approval", approvedStep1.CurrentStepLabel);

        var step1 = await db.ApprovalSteps.FirstAsync(s => s.ApprovalStepId == inFlightSteps[0].ApprovalStepId);
        var step2 = await db.ApprovalSteps.FirstAsync(s => s.ApprovalStepId == inFlightSteps[1].ApprovalStepId);

        Assert.Equal(ApprovalStepStatus.Approved, step1.StepStatus);
        Assert.Equal(ApprovalStepStatus.Pending, step2.StepStatus);
    }

    [SkippableFact]
    public async Task A_biometric_import_derives_a_late_marked_half_day_and_a_regularisation_approval_corrects_it()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using TimeLeaveDbContext db = _postgres.CreateContext(customerId, orgId);

        long employeeId = 3003;
        string deviceCode = "BIO-MAIN";
        string deviceUserId = "D-3003";

        // Map biometric device user to employee
        db.BiometricDeviceUsers.Add(new BiometricDeviceUser
        {
            OrgId = orgId,
            DeviceCode = deviceCode,
            DeviceUserId = deviceUserId,
            EmployeeId = employeeId
        });

        // Shift: 09:00 to 18:00, Break 60m, GraceIn 15m, HalfDayBelow 300m, AbsentBelow 180m
        var shift = new Shift
        {
            OrgId = orgId,
            Code = "STD",
            Name = "Standard Shift",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(18, 0),
            BreakMinutes = 60,
            GraceInMinutes = 15,
            GraceOutMinutes = 15,
            HalfDayBelowMinutes = 300,
            AbsentBelowMinutes = 120,
            IsActive = true
        };
        db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var engine = new LeaveCalculationEngine();
        var attendanceService = new AttendanceService(db, engine);

        var date = new DateOnly(2026, 8, 10); // A Monday

        // Punch in late at 10:30 AM (90 mins after 9:00 AM)
        var inTime = new DateTimeOffset(date.ToDateTime(new TimeOnly(10, 30)), TimeSpan.Zero);
        // Punch out early at 02:00 PM (14:00) -> total span 210 mins - break 60m = 150 mins worked (< 300m threshold)
        var outTime = new DateTimeOffset(date.ToDateTime(new TimeOnly(14, 0)), TimeSpan.Zero);

        var importReq = new BiometricPunchImportRequest(
            deviceCode,
            new List<BiometricPunchImportItem>
            {
                new(deviceUserId, inTime, deviceCode),
                new(deviceUserId, outTime, deviceCode)
            });

        int imported = await attendanceService.ImportBiometricPunchesAsync(importReq);
        Assert.Equal(2, imported);

        // Check derived daily attendance: should be HalfDay with 90 LateMinutes
        var derived = await db.DailyAttendances
            .FirstAsync(d => d.EmployeeId == employeeId && d.AttendanceDate == date);

        Assert.Equal(AttendanceStatus.HalfDay, derived.AttendanceStatus);
        Assert.Equal(AttendanceSource.Derived, derived.AttendanceSource);
        Assert.Equal(90, derived.LateMinutes);
        Assert.Equal(150, derived.WorkedMinutes);

        // Employee regularises the day: RequestedIn = 09:00, RequestedOut = 18:00, Status = Present
        var regReq = new CreateRegularisationRequest(
            employeeId,
            date,
            new DateTimeOffset(date.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero),
            new DateTimeOffset(date.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero),
            AttendanceStatus.Present,
            "Biometric device scanner failed on entry and exit");

        var regDto = await attendanceService.CreateRegularisationAsync(regReq);
        Assert.Equal(ApprovalStatus.InApproval, regDto.ApprovalStatus);

        // Manager approves regularisation
        await attendanceService.ApproveRegularisationAsync(regDto.RegularisationRequestId, Guid.NewGuid(), "Approved");

        // Verify corrected daily attendance: should now be Present, Regularised, LateMinutes = 0, WorkedMinutes = 480
        var corrected = await db.DailyAttendances
            .FirstAsync(d => d.EmployeeId == employeeId && d.AttendanceDate == date);

        Assert.Equal(AttendanceStatus.Present, corrected.AttendanceStatus);
        Assert.Equal(AttendanceSource.Regularised, corrected.AttendanceSource);
        Assert.Equal(0, corrected.LateMinutes);
        Assert.Equal(480, corrected.WorkedMinutes);
    }
}

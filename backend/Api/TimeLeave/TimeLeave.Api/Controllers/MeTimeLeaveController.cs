using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using TimeLeave.Api.Services;
using TimeLeave.Entity.Enums;
using TimeLeave.Entity.Models;
using TimeLeave.Entity.TableEntities;
using TimeLeave.Repository;

namespace TimeLeave.Api.Controllers;

/// <summary>
/// Employee self-service for leave, attendance, mobile punches, regularisation and overtime (H8, TK-55).
/// Resolves employee strictly from JWT claim ('sub'), never from an ID in the URL.
/// Accessible to any signed-in user without requiring administrative module permissions.
/// </summary>
[ApiController]
[Authorize]
[RequireApp(App.Hrms | App.Payroll)]
[Route("api/tla/me")]
[Route("api/me")]
public sealed class MeTimeLeaveController : ControllerBase
{
    private readonly TimeLeaveDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenant;
    private readonly IEmployeeClient _hrm;
    private readonly LeaveService _leave;
    private readonly AttendanceService _attendance;

    public MeTimeLeaveController(
        TimeLeaveDbContext db,
        ICurrentUser currentUser,
        ITenantContext tenant,
        IEmployeeClient hrm,
        LeaveService leave,
        AttendanceService attendance)
    {
        _db = db;
        _currentUser = currentUser;
        _tenant = tenant;
        _hrm = hrm;
        _leave = leave;
        _attendance = attendance;
    }

    private async Task<(long EmployeeId, long? ReportsToEmployeeId, long WorkLocationId)?> ResolveCurrentEmployeeAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        var profile = await _hrm.FindByUserIdAsync(customerId, orgId, userId, ct);
        if (profile is null) return null;

        return (profile.EmployeeId, profile.ReportsToEmployeeId, profile.WorkLocationId);
    }

    [HttpGet("leave/balances")]
    public async Task<IActionResult> GetLeaveBalances([FromQuery] int? year, CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        int y = year ?? DateTime.UtcNow.Year;
        return Ok(await _leave.GetBalancesAsync(emp.Value.EmployeeId, y, ct));
    }

    [HttpGet("leave/applications")]
    public async Task<IActionResult> GetLeaveApplications(CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var list = await _db.LeaveApplications
            .AsNoTracking()
            .Include(a => a.LeaveType)
            .Where(a => a.EmployeeId == emp.Value.EmployeeId)
            .OrderByDescending(a => a.FromDate)
            .Select(a => new LeaveApplicationDto(
                a.LeaveApplicationId,
                a.EmployeeId,
                a.LeaveTypeId,
                a.LeaveType != null ? a.LeaveType.Code : "",
                a.LeaveType != null ? a.LeaveType.Name : "",
                a.FromDate,
                a.ToDate,
                a.FromHalf,
                a.ToHalf,
                a.Days,
                a.Reason,
                a.AttachmentKey,
                a.LeaveStatus,
                a.ApprovalStatus,
                a.CurrentStepLabel,
                a.CurrentApproverEmployeeId))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost("leave/apply")]
    public async Task<IActionResult> ApplyLeave([FromBody] ApplyLeaveSelfRequest req, CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var applyReq = new ApplyLeaveRequest(
            emp.Value.EmployeeId,
            req.LeaveTypeId,
            req.FromDate,
            req.ToDate,
            req.FromHalf ? LeaveHalf.FirstHalf : LeaveHalf.Full,
            req.ToHalf ? LeaveHalf.SecondHalf : LeaveHalf.Full,
            req.Reason ?? "Leave application",
            req.AttachmentKey,
            emp.Value.ReportsToEmployeeId
        );

        var result = await _leave.ApplyLeaveAsync(applyReq, _currentUser.UserId, ct);
        return Ok(result);
    }

    [HttpPost("leave/{id:long}/cancel")]
    public async Task<IActionResult> CancelLeave(long id, CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var app = await _db.LeaveApplications.FirstOrDefaultAsync(a => a.LeaveApplicationId == id && a.EmployeeId == emp.Value.EmployeeId, ct);
        if (app is null) return NotFound();

        await _leave.CancelLeaveAsync(id, ct);
        return Ok(new { message = "Leave application cancelled." });
    }

    [HttpGet("attendance")]
    public async Task<IActionResult> GetAttendance(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        DateOnly fromDate;
        DateOnly toDate;

        if (from.HasValue && to.HasValue)
        {
            fromDate = from.Value;
            toDate = to.Value;
        }
        else
        {
            int y = year ?? DateTime.UtcNow.Year;
            int m = month ?? DateTime.UtcNow.Month;
            fromDate = new DateOnly(y, m, 1);
            toDate = fromDate.AddMonths(1).AddDays(-1);
        }

        var records = await _attendance.GetAttendanceRangeAsync(emp.Value.EmployeeId, fromDate, toDate, ct);
        var summary = await _attendance.GetMonthlyAttendanceSummaryAsync(emp.Value.EmployeeId, fromDate.Year, fromDate.Month, ct);

        return Ok(new
        {
            summary,
            records
        });
    }

    [HttpGet("punches")]
    public async Task<IActionResult> GetPunches([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        DateOnly d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(await _attendance.GetPunchesForDateAsync(emp.Value.EmployeeId, d, ct));
    }

    [HttpPost("punches/mobile")]
    public async Task<IActionResult> MobilePunch([FromBody] MobilePunchSelfRequest req, CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var punchReq = new RecordPunchRequest(
            emp.Value.EmployeeId,
            DateTimeOffset.UtcNow,
            PunchSource.Mobile,
            req.DeviceInfo,
            req.Latitude,
            req.Longitude
        );

        var punch = await _attendance.RecordPunchAsync(punchReq, ct);
        return Ok(punch);
    }

    [HttpGet("regularisation")]
    public async Task<IActionResult> GetRegularisation(CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        return Ok(await _attendance.GetRegularisationRequestsAsync(emp.Value.EmployeeId, ct));
    }

    [HttpPost("regularisation")]
    public async Task<IActionResult> SubmitRegularisation([FromBody] SubmitRegularisationSelfRequest req, CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var regReq = new CreateRegularisationRequest(
            emp.Value.EmployeeId,
            req.AttendanceDate,
            req.RequestedIn,
            req.RequestedOut,
            req.RequestedStatus,
            req.Reason,
            emp.Value.ReportsToEmployeeId
        );

        var result = await _attendance.CreateRegularisationAsync(regReq, ct);
        return Ok(result);
    }

    [HttpGet("overtime")]
    public async Task<IActionResult> GetOvertime(CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        return Ok(await _attendance.GetOvertimeRequestsAsync(emp.Value.EmployeeId, ct));
    }

    [HttpPost("overtime")]
    public async Task<IActionResult> SubmitOvertime([FromBody] SubmitOvertimeSelfRequest req, CancellationToken ct)
    {
        var emp = await ResolveCurrentEmployeeAsync(ct);
        if (emp is null) return NotFound(new { message = "No active employee profile linked to your user account." });

        var otReq = new CreateOvertimeRequest(
            emp.Value.EmployeeId,
            req.AttendanceDate,
            req.Minutes,
            req.OvertimeRate,
            emp.Value.ReportsToEmployeeId
        );

        var result = await _attendance.CreateOvertimeAsync(otReq, ct);
        return Ok(result);
    }
}

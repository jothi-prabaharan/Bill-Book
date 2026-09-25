using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Security;
using TimeLeave.Api.Services;
using TimeLeave.Entity.Models;

namespace TimeLeave.Api.Controllers;

[ApiController]
[Route("api/tla/attendance")]
[RequireApp(App.Hrms)]
[Authorize]
[RequireModulePermission("attendance")]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceService _attendanceService;
    private readonly ICurrentUser _currentUser;

    public AttendanceController(AttendanceService attendanceService, ICurrentUser currentUser)
    {
        _attendanceService = attendanceService;
        _currentUser = currentUser;
    }

    [HttpGet("shifts")]
    public async Task<ActionResult<List<ShiftDto>>> GetShifts(CancellationToken ct)
    {
        return Ok(await _attendanceService.GetShiftsAsync(ct));
    }

    [HttpPost("shifts")]
    public async Task<ActionResult<ShiftDto>> CreateShift([FromBody] CreateShiftRequest req, CancellationToken ct)
    {
        var result = await _attendanceService.CreateShiftAsync(req, ct);
        return Created($"/api/tla/attendance/shifts/{result.ShiftId}", result);
    }

    [HttpGet("weekly-offs")]
    public async Task<ActionResult<List<WeeklyOffPolicyDto>>> GetWeeklyOffs(CancellationToken ct)
    {
        return Ok(await _attendanceService.GetWeeklyOffPoliciesAsync(ct));
    }

    [HttpPost("weekly-offs")]
    public async Task<ActionResult<WeeklyOffPolicyDto>> CreateWeeklyOff([FromBody] CreateWeeklyOffPolicyRequest req, CancellationToken ct)
    {
        var result = await _attendanceService.CreateWeeklyOffPolicyAsync(req, ct);
        return Created($"/api/tla/attendance/weekly-offs/{result.WeeklyOffPolicyId}", result);
    }

    [HttpGet("holidays")]
    public async Task<ActionResult<List<HolidayListDto>>> GetHolidays([FromQuery] int year, CancellationToken ct)
    {
        return Ok(await _attendanceService.GetHolidayListsAsync(year, ct));
    }

    [HttpPost("holidays/lists")]
    public async Task<ActionResult<HolidayListDto>> CreateHolidayList([FromBody] CreateHolidayListRequest req, CancellationToken ct)
    {
        var result = await _attendanceService.CreateHolidayListAsync(req, ct);
        return Created($"/api/tla/attendance/holidays/lists/{result.HolidayListId}", result);
    }

    [HttpPost("holidays/lists/{id:long}/holidays")]
    public async Task<ActionResult<HolidayDto>> AddHoliday([FromRoute] long id, [FromBody] CreateHolidayRequest req, CancellationToken ct)
    {
        var result = await _attendanceService.AddHolidayAsync(id, req, ct);
        return Created($"/api/tla/attendance/holidays/lists/{id}/holidays/{result.HolidayId}", result);
    }

    [HttpPost("rosters/assign")]
    public async Task<IActionResult> AssignRoster([FromBody] AssignRosterRequest req, CancellationToken ct)
    {
        await _attendanceService.AssignRosterAsync(req, ct);
        return Ok();
    }

    [HttpPost("punches")]
    public async Task<ActionResult<PunchDto>> RecordPunch([FromBody] RecordPunchRequest req, CancellationToken ct)
    {
        var result = await _attendanceService.RecordPunchAsync(req, ct);
        return Ok(result);
    }

    [HttpPost("punches/biometric-import")]
    public async Task<ActionResult<int>> ImportBiometricPunches([FromBody] BiometricPunchImportRequest req, CancellationToken ct)
    {
        int count = await _attendanceService.ImportBiometricPunchesAsync(req, ct);
        return Ok(count);
    }

    [HttpGet("daily")]
    public async Task<ActionResult<List<DailyAttendanceDto>>> GetAttendanceRange([FromQuery] long employeeId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        return Ok(await _attendanceService.GetAttendanceRangeAsync(employeeId, from, to, ct));
    }

    [HttpPost("regularisations")]
    public async Task<ActionResult<RegularisationRequestDto>> CreateRegularisation([FromBody] CreateRegularisationRequest req, CancellationToken ct)
    {
        var result = await _attendanceService.CreateRegularisationAsync(req, ct);
        return Ok(result);
    }

    [HttpPost("regularisations/{id:long}/approve")]
    public async Task<IActionResult> ApproveRegularisation([FromRoute] long id, [FromBody] ApprovalActionRequest? req, CancellationToken ct)
    {
        await _attendanceService.ApproveRegularisationAsync(id, _currentUser.UserId, req?.Comments, ct);
        return Ok();
    }

    [HttpPost("overtime")]
    public async Task<ActionResult<OvertimeRequestDto>> CreateOvertime([FromBody] CreateOvertimeRequest req, CancellationToken ct)
    {
        var result = await _attendanceService.CreateOvertimeAsync(req, ct);
        return Ok(result);
    }

    [HttpPost("overtime/{id:long}/approve")]
    public async Task<IActionResult> ApproveOvertime([FromRoute] long id, CancellationToken ct)
    {
        await _attendanceService.ApproveOvertimeAsync(id, _currentUser.UserId, ct);
        return Ok();
    }
}

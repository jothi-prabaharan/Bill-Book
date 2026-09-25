using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using TimeLeave.Api.Services;
using TimeLeave.Entity.Models;

namespace TimeLeave.Api.Controllers;

[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/tla")]
public sealed class InternalTimeLeaveController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly AttendanceService _attendanceService;

    public InternalTimeLeaveController(TenantContext tenant, AttendanceService attendanceService)
    {
        _tenant = tenant;
        _attendanceService = attendanceService;
    }

    [HttpGet("attendance/monthly-paid-days")]
    public async Task<ActionResult<MonthlyPaidDaysDto>> GetMonthlyPaidDays(
        [FromQuery] Guid customerId,
        [FromQuery] Guid orgId,
        [FromQuery] long employeeId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct)
    {
        _tenant.CustomerId = customerId;
        _tenant.OrgId = orgId;

        var result = await _attendanceService.GetMonthlyAttendanceSummaryAsync(employeeId, year, month, ct);
        return Ok(result);
    }
}

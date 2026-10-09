using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Security;
using Site.Entity.Enums;
using Site.Repository.Repositories;

namespace Site.Api.Controllers;

public sealed class ApproveAttendanceRequest
{
    public decimal? OtHoursApproved { get; set; }
}

public sealed class RejectAttendanceRequest
{
    public string Reason { get; set; } = string.Empty;
}

[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/site/attendance")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class SiteAttendanceController : ControllerBase
{
    private readonly SiteAttendanceRepository _repo;
    private readonly ICurrentUser _currentUser;

    public SiteAttendanceController(
        SiteAttendanceRepository repo,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    [HttpGet("pending")]
    [RequirePermission("site.view")]
    public async Task<IActionResult> ListPending(
        [FromQuery] long? siteId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        var list = await _repo.ListPendingAttendanceAsync(siteId, fromDate, toDate, ct);
        return Ok(list);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("site.view")]
    public async Task<IActionResult> GetDetails(long id, CancellationToken ct)
    {
        var details = await _repo.GetAttendanceDetailsAsync(id, expectedLaborerId: null, ct);
        if (details == null)
        {
            return NotFound();
        }

        return Ok(details);
    }

    [HttpPost("{id:long}/approve")]
    [RequirePermission("site.approve")]
    public async Task<IActionResult> Approve(
        long id,
        [FromBody] ApproveAttendanceRequest? request,
        CancellationToken ct)
    {
        long? userId = null; // System/user id from claims or null

        try
        {
            var attendance = await _repo.ApproveClockOutAsync(
                id,
                request?.OtHoursApproved,
                userId,
                ct);

            return Ok(attendance);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/reject")]
    [RequirePermission("site.approve")]
    public async Task<IActionResult> Reject(
        long id,
        [FromBody] RejectAttendanceRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return BadRequest(new { message = "Rejection reason is required." });
        }

        long? userId = null;

        try
        {
            var attendance = await _repo.RejectClockOutAsync(
                id,
                request.Reason,
                userId,
                ct);

            return Ok(attendance);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}

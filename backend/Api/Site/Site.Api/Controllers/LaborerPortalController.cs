using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Site.Entity.Enums;
using Site.Repository.Repositories;

namespace Site.Api.Controllers;

public sealed class CheckInPortalRequest
{
    public long AssignmentId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public sealed class ClockOutPortalRequest
{
    public long AttendanceId { get; set; }
    public decimal DayFraction { get; set; } = 1.0m;
    public decimal OtHoursClaimed { get; set; } = 0m;
    public string WorkSummary { get; set; } = string.Empty;
}

[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/site/portal")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class LaborerPortalController : ControllerBase
{
    private readonly SiteAttendanceRepository _repo;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public LaborerPortalController(
        SiteAttendanceRepository repo,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _repo = repo;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    private async Task<long?> ResolveLaborerIdAsync(long? requestedLaborerId, CancellationToken ct)
    {
        if (_currentUser.UserId.HasValue)
        {
            var linked = await _repo.GetLaborerByUserIdAsync(_currentUser.UserId.Value, ct);
            if (linked != null)
            {
                return linked.LaborerId;
            }
        }

        return requestedLaborerId;
    }

    [HttpGet("assignments")]
    public async Task<IActionResult> GetMyAssignments([FromQuery] long? laborerId, CancellationToken ct)
    {
        long? targetLaborerId = await ResolveLaborerIdAsync(laborerId, ct);
        if (!targetLaborerId.HasValue)
        {
            return BadRequest(new { message = "No laborer profile is linked to the authenticated user account." });
        }

        var assignments = await _repo.GetLaborerActiveAssignmentsAsync(targetLaborerId.Value, Today, ct);
        return Ok(assignments);
    }

    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInPortalRequest request, CancellationToken ct)
    {
        if (request.AssignmentId <= 0)
        {
            return BadRequest(new { message = "AssignmentId is required." });
        }

        long? laborerId = await ResolveLaborerIdAsync(null, ct);
        try
        {
            var attendance = await _repo.CheckInAsync(
                request.AssignmentId,
                request.Latitude,
                request.Longitude,
                laborerId,
                Today,
                ct);

            return CreatedAtAction(nameof(GetAttendance), new { attendanceId = attendance.AttendanceId }, attendance);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost("clock-out")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ClockOut(
        [FromForm] ClockOutPortalRequest request,
        [FromForm] List<IFormFile> photos,
        CancellationToken ct)
    {
        if (request.AttendanceId <= 0)
        {
            return BadRequest(new { message = "AttendanceId is required." });
        }

        if (photos == null || photos.Count == 0)
        {
            return BadRequest(new { message = "At least one work-proof photo is required." });
        }

        if (request.DayFraction != 0.5m && request.DayFraction != 1.0m)
        {
            return BadRequest(new { message = "Day fraction must be 0.5 or 1.0 per policy." });
        }

        long? laborerId = await ResolveLaborerIdAsync(null, ct);

        var photoFiles = new List<UploadedPhotoFile>();
        foreach (var file in photos)
        {
            if (file.Length > 0)
            {
                photoFiles.Add(new UploadedPhotoFile(
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    file.OpenReadStream()));
            }
        }

        if (photoFiles.Count == 0)
        {
            return BadRequest(new { message = "No valid photo files provided." });
        }

        try
        {
            var attendance = await _repo.ClockOutAsync(
                request.AttendanceId,
                request.DayFraction,
                request.OtHoursClaimed,
                request.WorkSummary,
                photoFiles,
                laborerId,
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
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpGet("attendance/{attendanceId:long}")]
    public async Task<IActionResult> GetAttendance(long attendanceId, CancellationToken ct)
    {
        long? laborerId = await ResolveLaborerIdAsync(null, ct);
        try
        {
            var details = await _repo.GetAttendanceDetailsAsync(attendanceId, laborerId, ct);
            if (details == null)
            {
                return NotFound();
            }

            return Ok(details);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpGet("photos/{photoId:long}")]
    public async Task<IActionResult> GetPhoto(long photoId, CancellationToken ct)
    {
        var photo = await _repo.GetPhotoAsync(photoId, ct);
        if (photo == null)
        {
            return NotFound();
        }

        var stream = await _repo.OpenPhotoReadAsync(photo.StorageKey, ct);
        if (stream == null)
        {
            return NotFound();
        }

        return File(stream, photo.ContentType, photo.FileName);
    }
}

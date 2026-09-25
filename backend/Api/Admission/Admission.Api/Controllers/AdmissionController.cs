using Admission.Api.Services;
using Admission.Entity.Enums;
using Admission.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Admission.Api.Controllers;

/// <summary>
/// Enquiries and applications (S2, TK-62). Reading takes <c>admission.view</c>,
/// adding <c>admission.create</c>, changing and moving stages
/// <c>admission.edit</c>, and admitting <c>admission.approve</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("admission")]
[RequireApp(App.School)]
[Route("api/admission")]
public sealed class AdmissionController : ControllerBase
{
    private readonly EnquiryService _enquiries;
    private readonly ApplicationService _applications;

    public AdmissionController(EnquiryService enquiries, ApplicationService applications)
    {
        _enquiries = enquiries;
        _applications = applications;
    }

    [HttpGet("enquiries")]
    public async Task<IActionResult> Enquiries([FromQuery] EnquiryStatus? status, CancellationToken ct) =>
        Ok(await _enquiries.ListAsync(status, ct));

    [HttpPost("enquiries")]
    public async Task<IActionResult> CreateEnquiry([FromBody] SaveEnquiryRequest request, CancellationToken ct) =>
        Answer(await _enquiries.SaveAsync(null, request, ct));

    [HttpPut("enquiries/{id:long}")]
    public async Task<IActionResult> UpdateEnquiry(long id, [FromBody] SaveEnquiryRequest request, CancellationToken ct) =>
        Answer(await _enquiries.SaveAsync(id, request, ct));

    [HttpGet("applications")]
    public async Task<IActionResult> Applications([FromQuery] ApplicationStage? stage, CancellationToken ct) =>
        Ok(await _applications.ListAsync(stage, ct));

    [HttpGet("applications/{id:long}")]
    public async Task<IActionResult> Application(long id, CancellationToken ct) =>
        await _applications.GetAsync(id, ct) is ApplicationView view ? Ok(view) : NotFound();

    [HttpPost("applications")]
    public async Task<IActionResult> CreateApplication([FromBody] SaveApplicationRequest request, CancellationToken ct) =>
        Answer(await _applications.SaveAsync(null, request, ct));

    [HttpPut("applications/{id:long}")]
    public async Task<IActionResult> UpdateApplication(long id, [FromBody] SaveApplicationRequest request, CancellationToken ct) =>
        Answer(await _applications.SaveAsync(id, request, ct));

    [HttpPost("applications/{id:long}/stage")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Move(long id, [FromBody] MoveApplicationRequest request, CancellationToken ct) =>
        Answer(await _applications.MoveAsync(id, request, ct));

    /// <summary>Admits an offered application. Idempotent: a second call returns the same student.</summary>
    [HttpPost("applications/{id:long}/admit")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Admit(long id, [FromBody] AdmitRequest request, CancellationToken ct) =>
        Answer(await _applications.AdmitAsync(id, request, ct));

    // A row outside the caller's branch is not found: the query filter and RLS
    // hide it, so "another branch's" and "none" are one answer.
    private IActionResult Answer(AdmissionResult result) => result.Outcome switch
    {
        AdmissionOutcome.Ok => Ok(result.Body ?? new { id = result.Id }),
        AdmissionOutcome.NotFound => NotFound(),
        AdmissionOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new AdmissionMessage("The student records or contacts could not be reached just now. Try again shortly; nothing will be made twice.")),
        _ => UnprocessableEntity(new AdmissionMessage(result.Detail ?? "That change is not allowed.")),
    };
}

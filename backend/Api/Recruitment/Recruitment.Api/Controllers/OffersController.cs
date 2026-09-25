using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Services;
using Recruitment.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Recruitment.Api.Controllers;

[ApiController]
[Authorize]
[RequireApp(App.Hrms)]
[RequireModulePermission("recruitment")]
[Route("api/rec/offers")]
public sealed class OffersController : ControllerBase
{
    private readonly RecruitmentService _recruitment;

    public OffersController(RecruitmentService recruitment) => _recruitment = recruitment;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? applicationId = null, CancellationToken ct = default) =>
        Ok(await _recruitment.ListOffersAsync(applicationId, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var offer = await _recruitment.GetOfferByIdAsync(id, ct);
        return offer is null ? NotFound() : Ok(offer);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOfferRequest request, CancellationToken ct)
    {
        var offer = await _recruitment.CreateOfferAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = offer.OfferId }, offer);
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken ct) =>
        await _recruitment.ApproveOfferAsync(id, ct) ? Ok(new { success = true }) : NotFound();

    [HttpPost("{id:long}/send")]
    public async Task<IActionResult> Send(long id, CancellationToken ct) =>
        await _recruitment.SendOfferAsync(id, ct) ? Ok(new { success = true }) : NotFound();

    [HttpPost("{id:long}/decline")]
    public async Task<IActionResult> Decline(long id, CancellationToken ct) =>
        await _recruitment.DeclineOfferAsync(id, ct) ? Ok(new { success = true }) : NotFound();

    [HttpPost("{id:long}/revoke")]
    public async Task<IActionResult> Revoke(long id, CancellationToken ct) =>
        await _recruitment.RevokeOfferAsync(id, ct) ? Ok(new { success = true }) : NotFound();

    /// <summary>
    /// Accepts the offer and provisions the employee in HRM and salary structure in Payroll.
    /// Idempotent: Can be called multiple times without duplicate employee creation.
    /// </summary>
    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, CancellationToken ct)
    {
        var result = await _recruitment.AcceptOfferAsync(id, ct);
        return Ok(result);
    }
}

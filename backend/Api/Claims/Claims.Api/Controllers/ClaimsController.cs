using Claims.Api.Services;
using Claims.Entity.Enums;
using Claims.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Claims.Api.Controllers;

[ApiController]
[Authorize]
[RequireApp(App.Hrms)]
[RequireModulePermission("claims")]
[Route("api/clm/claims")]
public sealed class ClaimsController : ControllerBase
{
    private readonly ClaimService _claimService;

    public ClaimsController(ClaimService claimService) => _claimService = claimService;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long? employeeId,
        [FromQuery] ClaimStatus? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct) =>
        Ok(await _claimService.ListClaimsAsync(employeeId, status, fromDate, toDate, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var claim = await _claimService.GetClaimByIdAsync(id, ct);
        return claim is null ? NotFound() : Ok(claim);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseClaimRequest req, CancellationToken ct)
    {
        var claim = await _claimService.CreateClaimAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = claim.ExpenseClaimId }, claim);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateExpenseClaimRequest req, CancellationToken ct)
    {
        var claim = await _claimService.UpdateClaimAsync(id, req, ct);
        return claim is null ? NotFound() : Ok(claim);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        await _claimService.DeleteClaimAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct) =>
        Ok(await _claimService.SubmitClaimAsync(id, ct));

    [HttpPost("{id:long}/payout")]
    public async Task<IActionResult> Payout(long id, [FromBody] PayoutClaimRequest req, CancellationToken ct) =>
        Ok(await _claimService.PayoutClaimAsync(id, req, ct));
}

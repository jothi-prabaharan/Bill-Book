using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// Settings › Closing dates. How far back the books are closed, per role.
///
/// <b>A soft close.</b> One date per branch per role, so a month can be shut to
/// everyone who keys documents while staying open to whoever has to make the
/// adjusting entries the close itself produced.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/period-locks")]
public sealed class PeriodLocksController : ControllerBase
{
    private readonly PeriodLockService _locks;

    public PeriodLocksController(PeriodLockService locks) => _locks = locks;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _locks.ListAsync(ct));

    /// <summary>
    /// What the calling user may still post. A screen binds its date picker's
    /// minimum to this, so the refusal is prevented rather than reported.
    /// </summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        DateOnly? lockedUpto = await _locks.LockedUptoAsync(ct);

        return Ok(new PeriodLockStatus
        {
            LockedUpto = lockedUpto,
            OpenFrom = lockedUpto?.AddDays(1),
        });
    }

    [HttpPut]
    public async Task<IActionResult> Set(
        [FromBody] SavePeriodLockRequest request, CancellationToken ct) =>
        Ok(new { periodLockId = await _locks.SetAsync(request, ct) });

    /// <summary>Removes a role's lock, reopening every period for it.</summary>
    [HttpDelete("{roleId:int}")]
    public async Task<IActionResult> Clear(int roleId, CancellationToken ct) =>
        await _locks.ClearAsync(roleId, ct) ? NoContent() : NotFound();
}

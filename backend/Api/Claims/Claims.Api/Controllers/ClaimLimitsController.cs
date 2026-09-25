using Claims.Api.Services;
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
[Route("api/clm/limits")]
public sealed class ClaimLimitsController : ControllerBase
{
    private readonly ClaimService _claimService;

    public ClaimLimitsController(ClaimService claimService) => _claimService = claimService;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? categoryId, CancellationToken ct) =>
        Ok(await _claimService.GetLimitsAsync(categoryId, ct));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveClaimLimitRequest req, CancellationToken ct) =>
        Ok(await _claimService.SaveLimitAsync(req, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        await _claimService.DeleteLimitAsync(id, ct) ? NoContent() : NotFound();
}

using Hrm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Hrm.Api.Controllers;

[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/hrm/employees")]
public sealed class InternalLifecycleController : ControllerBase
{
    private readonly LifecycleService _lifecycle;

    public InternalLifecycleController(LifecycleService lifecycle) => _lifecycle = lifecycle;

    [HttpPost("{employeeId:long}/settle")]
    public async Task<IActionResult> SettleEmployee(long employeeId, [FromQuery] DateOnly? lastWorkingDate, CancellationToken ct)
    {
        await _lifecycle.SettleSeparationAsync(employeeId, lastWorkingDate, ct);
        return NoContent();
    }
}

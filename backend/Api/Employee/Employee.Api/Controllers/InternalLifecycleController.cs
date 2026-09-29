using Employee.Api.Services;
using Employee.Repository.Repositories;
using Employee.Repository.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Employee.Api.Controllers;

[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/hrm/employees")]
public sealed class InternalLifecycleController : ControllerBase
{
    private readonly LifecycleRepository _lifecycle;

    public InternalLifecycleController(LifecycleRepository lifecycle) => _lifecycle = lifecycle;

    [HttpPost("{employeeId:long}/settle")]
    public async Task<IActionResult> SettleEmployee(long employeeId, [FromQuery] DateOnly? lastWorkingDate, CancellationToken ct)
    {
        await _lifecycle.SettleSeparationAsync(employeeId, lastWorkingDate, ct);
        return NoContent();
    }
}

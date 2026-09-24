using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payroll.Api.Services;
using Payroll.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Payroll.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("payroll")]
[RequireApp(App.Payroll)]
[Route("api/payroll/fnf")]
public sealed class FnfController : ControllerBase
{
    private readonly FnfSettlementService _fnf;

    public FnfController(FnfSettlementService fnf) => _fnf = fnf;

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetSettlement(long id, CancellationToken ct)
    {
        var settlement = await _fnf.GetSettlementAsync(id, ct);
        return settlement is null ? NotFound() : Ok(settlement);
    }

    [HttpGet("employee/{employeeId:long}")]
    public async Task<IActionResult> GetSettlementByEmployee(long employeeId, CancellationToken ct)
    {
        var settlement = await _fnf.GetSettlementByEmployeeAsync(employeeId, ct);
        return settlement is null ? NotFound() : Ok(settlement);
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] CreateFnfSettlementRequest request, CancellationToken ct) =>
        Ok(new { id = await _fnf.CalculateAndCreateSettlementAsync(request, ct) });

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken ct)
    {
        await _fnf.ApproveSettlementAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, [FromQuery] Guid? linkedUserId, CancellationToken ct) =>
        Ok(new { runId = await _fnf.PostSettlementAsync(id, linkedUserId, ct) });
}

using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// How far back a branch's books are closed, for another service.
///
/// The caller is the costing worker, which restates the cost of stock-outs
/// when a weighted average is recalculated and must leave alone every one in a
/// closed period. It holds no user token, so the tenant travels in the query
/// the same way it travels in the body of a ledger posting.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/period-locks")]
public sealed class InternalPeriodLocksController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalPeriodLocksController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    /// <summary>
    /// The branch's strictest lock: the latest date closed to any role. A
    /// worker is no role, and a period closed to anybody is one it must not
    /// restate.
    /// </summary>
    [HttpGet("branch")]
    public async Task<IActionResult> Branch(
        [FromQuery] Guid customerId, [FromQuery] Guid orgId, CancellationToken ct)
    {
        if (customerId == Guid.Empty || orgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse
            {
                Message = "A customer and an organization are required to read a period lock.",
            });
        }

        // Set before anything resolves a DbContext: the context is built from
        // the tenant, so resolving the service first would bind it to no tenant.
        _tenant.CustomerId = customerId;
        _tenant.OrgId = orgId;

        var locks = _services.GetRequiredService<PeriodLockService>();

        DateOnly? lockedUpto = await locks.BranchLockedUptoAsync(ct);

        return Ok(new PeriodLockStatus
        {
            LockedUpto = lockedUpto,
            OpenFrom = lockedUpto?.AddDays(1),
        });
    }
}

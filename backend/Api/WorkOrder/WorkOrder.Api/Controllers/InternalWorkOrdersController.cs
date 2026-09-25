using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using WorkOrder.Api.Services;
using WorkOrder.Entity.Models;

namespace WorkOrder.Api.Controllers;

/// <summary>
/// Raises a work order for another School service (TK-67 plan occurrences,
/// TK-68 AMC visits), once per source key. The branch comes in the body.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/work-orders")]
public sealed class InternalWorkOrdersController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalWorkOrdersController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("raise")]
    public async Task<IActionResult> Raise([FromBody] RaiseWorkOrderRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest();
        }

        if (request.FacilityAssetId is null && request.SpaceId is null)
        {
            return UnprocessableEntity(new WorkOrderMessage("Say where the work is: an asset, a space, or both."));
        }

        return Ok(await _services.GetRequiredService<WorkOrderService>().RaiseAsync(request, ct));
    }
}

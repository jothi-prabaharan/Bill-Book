using Customer.Entity.Models;
using Customer.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Customer.Api.Controllers;

/// <summary>
/// The worker's service reminder run (TK-140): today's reminders, addressed and
/// worded, for the worker to send. Read-only; the tenant comes from the body,
/// set before the repository is resolved.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/service-plans")]
public sealed class InternalServicePlansController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalServicePlansController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("reminders")]
    public async Task<IActionResult> Reminders([FromBody] RunServiceRemindersRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new { message = "A customer and an organization are required." });
        }

        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var plans = _services.GetRequiredService<ServicePlanRepository>();
        return Ok(await plans.RemindersAsync(request.AsOf, ct));
    }
}

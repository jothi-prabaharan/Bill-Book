using Amc.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.School;
using Shared.Kernel.Tenancy;

namespace Amc.Api.Controllers;

/// <summary>
/// The contracts due a renewal reminder (TK-68), read daily by
/// Notification.Worker. The branch comes in the body.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/amc")]
public sealed class InternalAmcController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalAmcController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("renewals-due")]
    public async Task<IActionResult> RenewalsDue([FromBody] AmcRenewalQuery request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest();
        }

        return Ok(await _services.GetRequiredService<AmcService>().RenewalsDueAsync(request.On, ct));
    }
}

using Customer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Customer.Api.Controllers;

/// <summary>
/// Writes Customer's master data for a newly created organization: its SLA
/// policies (TK-18).
///
/// Called by provisioning, which holds no user token — so the tenant comes from
/// the request body and the endpoint is guarded by the shared internal key. The
/// same shape as every other service's. <b>Re-runnable</b>: it adds only the
/// priorities the branch is missing.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/seed")]
public sealed class InternalSeedController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;
    private readonly ILogger<InternalSeedController> _log;

    public InternalSeedController(
        TenantContext tenant, IServiceProvider services, ILogger<InternalSeedController> log)
    {
        _tenant = tenant;
        _services = services;
        _log = log;
    }

    [HttpPost("organization")]
    public async Task<IActionResult> SeedOrganization(
        [FromBody] SeedOrganizationRequest request, CancellationToken ct)
    {
        // Set before anything resolves a DbContext: the context is built from
        // the tenant, so resolving a service first would bind it to no tenant.
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var sla = _services.GetRequiredService<SlaPolicyService>();

        SeedOrganizationResponse response = new()
        {
            Seeded =
            {
                ["slaPolicies"] = await sla.SeedAsync(request.OrgId, ct),
            },
        };

        _log.LogInformation(
            "Seeded Customer for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);

        return Ok(response);
    }
}

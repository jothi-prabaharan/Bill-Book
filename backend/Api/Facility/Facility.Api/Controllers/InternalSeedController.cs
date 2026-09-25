using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Facility.Api.Services;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Facility.Api.Controllers;

/// <summary>
/// Writes Facility's master data for a branch (S5, TK-65). Called by Master's
/// tenant seeder with the internal key, so the branch comes in the body and is
/// set before anything resolves a context. Idempotent, so a retry re-seeds safely.
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

    public InternalSeedController(TenantContext tenant, IServiceProvider services, ILogger<InternalSeedController> log)
    {
        _tenant = tenant;
        _services = services;
        _log = log;
    }

    [HttpPost("organization")]
    public async Task<IActionResult> SeedOrganization([FromBody] SeedOrganizationRequest request, CancellationToken ct)
    {
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var response = new SeedOrganizationResponse
        {
            Seeded = await _services.GetRequiredService<FacilitySeeder>().SeedForOrganizationAsync(request.OrgId, ct),
        };

        _log.LogInformation("Seeded Facility for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);
        return Ok(response);
    }
}

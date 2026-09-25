using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaintenanceContract.Api.Services;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace MaintenanceContract.Api.Controllers;

/// <summary>
/// Writes MaintenanceContract's master data for a branch (S8, TK-68). Called by Master's
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
            Seeded = await _services.GetRequiredService<MaintenanceContractSeeder>().SeedForOrganizationAsync(request.OrgId, ct),
        };

        _log.LogInformation("Seeded MaintenanceContract for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);
        return Ok(response);
    }
}

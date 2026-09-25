using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Employee.Api.Services;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Employee.Api.Controllers;

/// <summary>
/// Writes Employee's master data for an organization: one department, designation,
/// grade and location, and the EMP series (TK-48).
///
/// Called by Master's tenant seeder, which holds no user token — so the tenant
/// comes from the request body and the endpoint is guarded by the shared
/// internal key rather than a JWT. The same shape as every other service's.
/// Idempotent, so the admin screen's retry re-seeds safely.
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
        // the tenant, so resolving the seeder first would bind it to no tenant.
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var seeder = _services.GetRequiredService<EmployeeSeeder>();

        var response = new SeedOrganizationResponse
        {
            Seeded = await seeder.SeedForOrganizationAsync(request.OrgId, ct),
        };

        _log.LogInformation(
            "Seeded Employee for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);

        return Ok(response);
    }
}

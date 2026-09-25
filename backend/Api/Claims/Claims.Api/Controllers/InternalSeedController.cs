using Claims.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Claims.Api.Controllers;

[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/clm/seed")]
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
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var seeder = _services.GetRequiredService<ClaimsSeeder>();
        var seeded = await seeder.SeedAsync(request.CustomerId, request.OrgId, ct);

        var response = new SeedOrganizationResponse
        {
            Seeded = seeded
        };

        _log.LogInformation(
            "Seeded Claims for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);

        return Ok(response);
    }
}

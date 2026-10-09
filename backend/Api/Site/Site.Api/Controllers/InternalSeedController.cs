using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using Site.Repository.Repositories;

namespace Site.Api.Controllers;

/// <summary>
/// Writes Site services master data for an organization (TK-169).
/// Called by Master's tenant seeder with the internal key.
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

        var seeder = _services.GetRequiredService<SiteSeederRepository>();
        int count = await seeder.SeedNumberingSeriesAsync(request.OrgId, ct);

        var response = new SeedOrganizationResponse
        {
            Seeded =
            {
                ["numberingSeries"] = count,
            },
        };

        _log.LogInformation("Seeded Site services for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);
        return Ok(response);
    }
}

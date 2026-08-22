using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Api.Services;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Purchase.Api.Controllers;

/// <summary>
/// Writes Purchase's master data for a newly created organization.
///
/// Called by provisioning, which holds no user token — so the tenant comes from
/// the request body and the endpoint is guarded by the shared internal key
/// rather than a JWT. The same shape as Accounting's, Contacts', Inventory's and
/// Sales'.
///
/// <b>Must run after Accounting</b>, which owns and migrates the numbering-series
/// table Purchase writes rows into.
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

        PurchaseSeeder seeder = _services.GetRequiredService<PurchaseSeeder>();

        SeedOrganizationResponse response = new()
        {
            Seeded =
            {
                ["numberingSeries"] = await seeder.SeedNumberingSeriesAsync(request.OrgId, ct),
            },
        };

        _log.LogInformation(
            "Seeded Purchase for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);

        return Ok(response);
    }
}

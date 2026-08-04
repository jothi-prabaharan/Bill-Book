using Banking.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Banking.Api.Controllers;

/// <summary>
/// Writes Banking's master data for a newly created organization.
///
/// Called by Platform's provisioning, which holds no user token — so the tenant
/// comes from the request body and the endpoint is guarded by the shared
/// internal key rather than a JWT. The same shape as Accounting's, Contacts' and
/// Inventory's.
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

        var seeder = _services.GetRequiredService<BankingSeeder>();

        var response = new SeedOrganizationResponse
        {
            Seeded =
            {
                ["numberingSeries"] = await seeder.SeedNumberingSeriesAsync(request.OrgId, ct),
            },
        };

        _log.LogInformation(
            "Seeded Banking for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);

        return Ok(response);
    }
}

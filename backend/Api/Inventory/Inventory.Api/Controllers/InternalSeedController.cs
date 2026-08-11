using Inventory.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Inventory.Api.Controllers;

/// <summary>
/// Writes Inventory's master data for a newly created organization: the unit
/// types with their units, and the standard metal purities.
///
/// The unit types matter most — an item requires one, so until this runs the
/// item master cannot save anything at all.
///
/// **Re-runnable, and meant to be.** Both seeders add only the rows the
/// organization is missing, so calling this against an organization set up
/// months ago backfills whatever has been added to the seed lists since.
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
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var uom = _services.GetRequiredService<UomService>();
        var purities = _services.GetRequiredService<MetalPurityService>();
        var seeder = _services.GetRequiredService<InventorySeeder>();

        var response = new SeedOrganizationResponse
        {
            Seeded =
            {
                ["unitTypesAndUnits"] = await uom.SeedForOrganizationAsync(request.OrgId, ct),
                ["metalPurities"] = await purities.SeedForOrganizationAsync(request.OrgId, ct),
                ["numberingSeries"] = await seeder.SeedNumberingSeriesAsync(request.OrgId, ct),
            },
        };

        _log.LogInformation(
            "Seeded Inventory for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);

        return Ok(response);
    }
}

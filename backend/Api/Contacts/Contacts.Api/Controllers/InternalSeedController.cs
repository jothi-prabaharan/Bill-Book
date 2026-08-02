using Contacts.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Contacts.Api.Controllers;

/// <summary>
/// Writes the contact person roles for a newly created organization. Called by
/// Platform's provisioning worker with no user token, so the tenant comes from
/// the body and the endpoint is guarded by a shared key.
///
/// **Re-runnable, and meant to be.** The seeder adds only the roles the
/// organization is missing, so calling this against an organization set up
/// months ago backfills whatever has been added to the seed list since.
/// </summary>
[ApiController]
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

        var roles = _services.GetRequiredService<ContactPersonRoleService>();

        var response = new SeedOrganizationResponse
        {
            Seeded = { ["contactPersonRoles"] = await roles.SeedForOrganizationAsync(request.OrgId, ct) },
        };

        _log.LogInformation(
            "Seeded Contacts for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);

        return Ok(response);
    }
}

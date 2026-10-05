using Shared.Kernel.Apps;
using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Master.Api.Controllers;

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

        var roles = _services.GetRequiredService<ContactPersonRoleRepository>();
        var contacts = _services.GetRequiredService<ContactRepository>();

        var response = new SeedOrganizationResponse
        {
            Seeded =
            {
                ["contactPersonRoles"] = await roles.SeedForOrganizationAsync(request.OrgId, ct),

                // The till's default customer (TK-17).
                ["walkInCustomer"] = await contacts.SeedWalkInAsync(
                    await contacts.BranchCurrencyAsync(ct), ct),
            },
        };

        _log.LogInformation(
            "Seeded Contacts for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);

        return Ok(response);
    }

    /// <summary>
    /// Seeds a whole branch: every service its business type and add-ons call
    /// for, with the trade read from the branch row (TK-153). The same fan-out
    /// a branch created from the app gets, for a branch made where no service
    /// was up to seed it — the install's first branch, from
    /// <c>deploy/local/seed/first-branch.sh</c>. Idempotent like every seed.
    /// Answers 503 naming the services that could not be reached, so the
    /// caller retries until they are all up.
    /// </summary>
    [HttpPost("branch")]
    public async Task<IActionResult> SeedBranch(
        [FromBody] SeedOrganizationRequest request, CancellationToken ct)
    {
        IReadOnlyList<string> failed = await _services.GetRequiredService<ITenantSeeder>()
            .SeedAsync(request.CustomerId, request.OrgId, ct);

        if (failed.Count > 0)
        {
            _log.LogWarning("Seeding branch {OrgId} left {Failed} unseeded.", request.OrgId, failed);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { unseeded = failed });
        }

        return NoContent();
    }
}

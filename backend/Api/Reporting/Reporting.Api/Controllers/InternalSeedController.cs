using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Repository.SeedData;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Reporting.Api.Controllers;

/// <summary>
/// Writes Reporting's master data for a branch: the report catalog and the column
/// metadata beneath it.
///
/// Called by provisioning, which holds no user token — so the tenant comes from
/// the request body and is set here, and the endpoint is guarded by the shared
/// internal key rather than a JWT.
///
/// <b>Re-runnable, and meant to be.</b> The catalog grows with each stage and
/// forty-five reports will not arrive on the same day, so calling this against a
/// branch set up months ago backfills every report added since. The counts are
/// what was actually written, which is nothing on a branch already complete.
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
        // Set before anything resolves a DbContext: the context is built from the
        // resolved tenant, so a seeder resolved first would bind to no branch and
        // write rows the query filter then hides from everyone.
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        ReportCatalogSeeder seeder = _services.GetRequiredService<ReportCatalogSeeder>();

        Dictionary<string, int> written = await seeder.SeedAsync(ct);

        _log.LogInformation(
            "Seeded the report catalog for organization {OrgId}: {Count} rows written",
            request.OrgId,
            written.Values.Sum());

        return Ok(new SeedOrganizationResponse { Seeded = written });
    }
}

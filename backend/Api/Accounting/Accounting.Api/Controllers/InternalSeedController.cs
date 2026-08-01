using Accounting.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Writes Accounting's master data for a newly created organization: the chart
/// of accounts, the GST rates, the numbering series and the payment terms.
///
/// Called by Platform's provisioning worker, which holds no user token — so the
/// tenant is taken from the request body and set here, and the endpoint is
/// guarded by a shared key rather than a JWT.
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
        // Set before anything resolves a DbContext: the context is built from
        // the tenant, so resolving a service first would bind it to no tenant.
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var accounts = _services.GetRequiredService<AccountService>();
        var taxes = _services.GetRequiredService<TaxMasterService>();
        var numbering = _services.GetRequiredService<NumberingSeriesService>();
        var terms = _services.GetRequiredService<PaymentTermService>();

        var response = new SeedOrganizationResponse
        {
            Seeded =
            {
                // Order matters only in that the chart of accounts comes first:
                // tax rates provision sub-accounts beneath its control accounts.
                ["accounts"] = await accounts.SeedForOrganizationAsync(request.OrgId, ct),
                ["taxMasters"] = await taxes.SeedForOrganizationAsync(request.OrgId, ct),
                ["numberingSeries"] = await numbering.SeedForOrganizationAsync(request.OrgId, ct),
                ["paymentTerms"] = await terms.SeedForOrganizationAsync(request.OrgId, ct),
            },
        };

        _log.LogInformation(
            "Seeded Accounting for organization {OrgId}: {@Seeded}", request.OrgId, response.Seeded);

        return Ok(response);
    }
}

using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Where Purchase puts a posted bill's capital lines on the fixed asset register
/// (D-19, TK-12). Purchase never touches <c>acc</c> itself (hard rule 8).
///
/// Guarded by the internal key, with the tenant in the body — set before the
/// service is resolved, because its <c>DbContext</c> is built from the tenant.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/fixed-assets")]
public sealed class InternalFixedAssetsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalFixedAssetsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    /// <summary>
    /// Registers each capital line and reclassifies its cost out of the shared
    /// Fixed Asset account. Safe to repeat: a line already registered is
    /// returned as it is.
    /// </summary>
    [HttpPost("capitalise-bill")]
    public async Task<IActionResult> CapitaliseBill(
        [FromBody] CapitaliseBillRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse
            {
                Message = "A customer and an organization are required.",
            });
        }

        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var assets = _services.GetRequiredService<FixedAssetService>();

        (FixedAssetResult result, CapitaliseBillResponse? response) =
            await assets.CapitaliseBillAsync(request, ct);

        return FixedAssetsController.Map(result, () => Ok(response));
    }
}

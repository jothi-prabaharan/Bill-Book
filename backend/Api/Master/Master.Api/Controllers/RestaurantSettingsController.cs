using Master.Entity.Models;
using Master.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Master.Api.Controllers;

/// <summary>Settings › Restaurant (D-39, TK-143): the branch's switches and billing rules.</summary>
[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[Route("api/restaurant/settings")]
public sealed class RestaurantSettingsController : ControllerBase
{
    private readonly RestaurantSettingsRepository _settings;

    public RestaurantSettingsController(RestaurantSettingsRepository settings) => _settings = settings;

    /// <summary>The effective settings: a required key counts only while its switch is on.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _settings.GetSettingsAsync(ct));

    /// <summary>
    /// Saving is a settings change as well as a restaurant one, so it needs
    /// <c>settings.edit</c> on top of the <c>restaurant.edit</c> the module guard asks for.
    /// </summary>
    [HttpPut]
    [PermissionAction("edit")]
    [RequirePermission("settings.edit")]
    public async Task<IActionResult> Update([FromBody] SaveRestaurantSettingsRequest request, CancellationToken ct)
    {
        (string? refusal, var settings) = await _settings.UpdateSettingsAsync(request, ct);
        return refusal is null ? Ok(settings) : BadRequest(new MessageResponse { Message = refusal });
    }
}

/// <summary>
/// The effective settings for Sales, Inventory and Reporting (TK-143), read
/// through <c>HttpRestaurantSettingsProvider</c>. Guarded by the internal key;
/// the branch comes from the body and is set before the repository is resolved.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/restaurant/settings")]
public sealed class InternalRestaurantSettingsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalRestaurantSettingsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost]
    public async Task<IActionResult> Get([FromBody] InternalBranchRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse { Message = "A customer and an organization are required." });
        }

        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        return Ok(await _services.GetRequiredService<RestaurantSettingsRepository>().GetSettingsAsync(ct));
    }
}

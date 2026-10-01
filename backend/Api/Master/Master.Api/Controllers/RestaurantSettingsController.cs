using Master.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Restaurant;

namespace Master.Api.Controllers;

[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[Route("api/restaurant/settings")]
public class RestaurantSettingsController : ControllerBase
{
    private readonly RestaurantSettingsRepository _repo;

    public RestaurantSettingsController(RestaurantSettingsRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    [PermissionAction("view")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings = await _repo.GetSettingsAsync(ct);
        return Ok(settings);
    }

    [HttpPut]
    [PermissionAction("edit")]
    public async Task<IActionResult> Update([FromBody] RestaurantSettingsView settings, CancellationToken ct)
    {
        await _repo.UpdateSettingsAsync(settings, ct);
        return Ok(settings);
    }
}

[ApiController]
[Authorize]
[Route("internal/restaurant/settings")]
public class InternalRestaurantSettingsController : ControllerBase
{
    private readonly RestaurantSettingsRepository _repo;

    public InternalRestaurantSettingsController(RestaurantSettingsRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings = await _repo.GetSettingsAsync(ct);
        return Ok(settings);
    }
}

using Microsoft.AspNetCore.Mvc;
using Inventory.Repository.Repositories;
using Inventory.Entity.TableEntities;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
public class MenuController : ControllerBase
{
    private readonly MenuRepository _menuRepo;

    public MenuController(MenuRepository menuRepo)
    {
        _menuRepo = menuRepo;
    }

    [HttpGet]
    [PermissionAction("view")]
    public async Task<IActionResult> GetMenu([FromQuery] OrderType orderType, [FromQuery] TimeSpan timeOfDay)
    {
        var menu = await _menuRepo.GetMenuAsync(orderType, timeOfDay);
        return Ok(menu);
    }

    [HttpPut("{id}/availability")]
    [PermissionAction("edit")]
    public async Task<IActionResult> FlipAvailability(long id, [FromBody] bool isAvailable)
    {
        await _menuRepo.FlipAvailabilityAsync(id, isAvailable);
        return NoContent();
    }
}

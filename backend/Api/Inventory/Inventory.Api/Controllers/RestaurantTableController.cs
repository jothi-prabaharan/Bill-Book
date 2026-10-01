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
public class RestaurantTableController : ControllerBase
{
    private readonly TableRepository _repo;

    public RestaurantTableController(TableRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    [PermissionAction("view")]
    public async Task<IActionResult> GetTables()
    {
        var tables = await _repo.GetTablesAsync();
        return Ok(tables);
    }

    [HttpPut("{id}/status")]
    [PermissionAction("edit")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] TableStatus status)
    {
        await _repo.UpdateTableStatusAsync(id, status);
        return NoContent();
    }
}

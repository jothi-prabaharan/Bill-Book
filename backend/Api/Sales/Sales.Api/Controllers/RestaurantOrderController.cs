using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sales.Entity.TableEntities;
using Sales.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
public class RestaurantOrderController : ControllerBase
{
    private readonly RestaurantOrderRepository _repo;

    public RestaurantOrderController(RestaurantOrderRepository repo)
    {
        _repo = repo;
    }

    [HttpPost]
    [PermissionAction("create")]
    public async Task<IActionResult> OpenOrder([FromBody] RestaurantOrder order)
    {
        var result = await _repo.OpenOrderAsync(order);
        return CreatedAtAction(nameof(OpenOrder), new { id = result.RestaurantOrderId }, result);
    }

    [HttpPost("{id}/lines")]
    [PermissionAction("edit")]
    public async Task<IActionResult> AddLine(long id, [FromBody] RestaurantOrderDetail line)
    {
        await _repo.AddLineAsync(id, line);
        return Ok();
    }

    [HttpDelete("{id}/lines/{lineId}")]
    [PermissionAction("edit")]
    public async Task<IActionResult> RemoveLine(long id, long lineId)
    {
        await _repo.RemoveLineAsync(id, lineId);
        return NoContent();
    }

    [HttpPut("{id}/type")]
    [PermissionAction("edit")]
    public async Task<IActionResult> ChangeOrderType(long id, [FromBody] OrderType newType)
    {
        await _repo.ChangeOrderTypeAsync(id, newType);
        return NoContent();
    }

    [HttpPut("{id}/table")]
    [PermissionAction("edit")]
    public async Task<IActionResult> MoveToTable(long id, [FromBody] long newTableId)
    {
        await _repo.MoveToTableAsync(id, newTableId);
        return NoContent();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sales.Repository.Repositories;
using Sales.Entity.TableEntities;
using Shared.Kernel.Apps;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
public class KotController : ControllerBase
{
    private readonly KotRepository _repo;

    public KotController(KotRepository repo)
    {
        _repo = repo;
    }

    [HttpPost("{orderId}/send")]
    [PermissionAction("create")]
    public async Task<IActionResult> SendLines(long orderId)
    {
        var result = await _repo.SendOrderLinesAsync(orderId);
        return Ok(result);
    }

    [HttpPost("{kotId}/reprint")]
    [PermissionAction("create")]
    public async Task<IActionResult> Reprint(long kotId)
    {
        await _repo.ReprintAsync(kotId);
        return Ok();
    }

    [HttpPut("lines/{lineId}/cancel")]
    [PermissionAction("edit")]
    public async Task<IActionResult> CancelLine(long lineId, [FromBody] string reason)
    {
        await _repo.CancelLineAsync(lineId, reason);
        return Ok();
    }

    [HttpPut("lines/{lineId}/status")]
    [PermissionAction("edit")]
    public async Task<IActionResult> MoveStatus(long lineId, [FromBody] KotLineStatus status)
    {
        await _repo.MoveLineStatusAsync(lineId, status);
        return Ok();
    }

    [HttpGet("station/{stationId}")]
    [PermissionAction("read")]
    public async Task<IActionResult> GetTicketsByStation(int stationId)
    {
        var result = await _repo.GetActiveTicketsByStationAsync((MenuStation)stationId);
        return Ok(result);
    }
}

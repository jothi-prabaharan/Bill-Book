using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("inventory")]
[Route("internal/item-serials")]
public class ItemSerialsController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public ItemSerialsController(InventoryDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] long? itemId, CancellationToken ct)
    {
        var query = _db.ItemSerials.AsNoTracking();
        
        if (itemId.HasValue)
        {
            query = query.Where(s => s.ItemId == itemId.Value);
        }
        
        var serials = await query
            .OrderBy(s => s.SerialNumber)
            .ToListAsync(ct);
            
        return Ok(serials);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetAsync(long id, CancellationToken ct)
    {
        var serial = await _db.ItemSerials
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ItemSerialId == id, ct);
            
        return serial is null ? NotFound() : Ok(serial);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ItemSerial serial, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _db.ItemSerials.Add(serial);
        await _db.SaveChangesAsync(ct);
        
        return CreatedAtAction(nameof(GetAsync), new { id = serial.ItemSerialId }, serial);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync(long id, [FromBody] ItemSerial update, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var serial = await _db.ItemSerials.FirstOrDefaultAsync(s => s.ItemSerialId == id, ct);
        if (serial is null) return NotFound();

        serial.SerialNumber = update.SerialNumber;
        serial.ItemBatchId = update.ItemBatchId;
        serial.HallmarkNumber = update.HallmarkNumber;
        serial.Status = update.Status;
        serial.WarehouseId = update.WarehouseId;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync(long id, CancellationToken ct)
    {
        var serial = await _db.ItemSerials.FirstOrDefaultAsync(s => s.ItemSerialId == id, ct);
        if (serial is null) return NotFound();

        _db.ItemSerials.Remove(serial);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

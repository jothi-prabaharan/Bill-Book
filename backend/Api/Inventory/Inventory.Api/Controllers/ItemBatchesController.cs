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
[Route("internal/item-batches")]
public class ItemBatchesController : ControllerBase
{
    private readonly InventoryDbContext _db;

    public ItemBatchesController(InventoryDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] long? itemId, CancellationToken ct)
    {
        var query = _db.ItemBatches.AsNoTracking();
        
        if (itemId.HasValue)
        {
            query = query.Where(b => b.ItemId == itemId.Value);
        }
        
        var batches = await query
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchNumber)
            .ToListAsync(ct);
            
        return Ok(batches);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetAsync(long id, CancellationToken ct)
    {
        var batch = await _db.ItemBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.ItemBatchId == id, ct);
            
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ItemBatch batch, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _db.ItemBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
        
        return CreatedAtAction(nameof(GetAsync), new { id = batch.ItemBatchId }, batch);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync(long id, [FromBody] ItemBatch update, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var batch = await _db.ItemBatches.FirstOrDefaultAsync(b => b.ItemBatchId == id, ct);
        if (batch is null) return NotFound();

        batch.BatchNumber = update.BatchNumber;
        batch.ManufactureDate = update.ManufactureDate;
        batch.ExpiryDate = update.ExpiryDate;
        batch.Mrp = update.Mrp;
        batch.SupplierBatchNumber = update.SupplierBatchNumber;
        batch.IsActive = update.IsActive;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync(long id, CancellationToken ct)
    {
        var batch = await _db.ItemBatches.FirstOrDefaultAsync(b => b.ItemBatchId == id, ct);
        if (batch is null) return NotFound();

        _db.ItemBatches.Remove(batch);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

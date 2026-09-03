using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("inventory")]
[Route("api/inventory/price-lists")]
public class PriceListsController : ControllerBase
{
    private readonly InventoryDbContext _context;
    private readonly ITenantContext _tenant;

    public PriceListsController(InventoryDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult> Get(CancellationToken cancellationToken)
    {
        var lists = await _context.PriceLists
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
            
        return Ok(lists);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var list = await _context.PriceLists
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            
        if (list == null) return NotFound();
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] PriceList request, CancellationToken cancellationToken)
    {
        request.Id = Guid.NewGuid();
        request.OrgId = _tenant.OrgId!.Value;
        
        _context.PriceLists.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        
        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] PriceList request, CancellationToken cancellationToken)
    {
        var list = await _context.PriceLists.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (list == null) return NotFound();

        list.Name = request.Name;
        list.Description = request.Description;
        list.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

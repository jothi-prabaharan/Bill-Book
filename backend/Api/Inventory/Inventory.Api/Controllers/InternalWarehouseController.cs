using System;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entity.Enums;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Inventory.Api.Controllers;

public sealed class CreateSiteWarehouseRequest
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public long SiteId { get; set; }
    public string SiteCode { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
}

public sealed class CreateSiteWarehouseResponse
{
    public long WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
}

/// <summary>
/// Internal endpoints for managing warehouses from other microservices (TK-170).
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/warehouses")]
public sealed class InternalWarehouseController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;
    private readonly ILogger<InternalWarehouseController> _log;

    public InternalWarehouseController(
        TenantContext tenant,
        IServiceProvider services,
        ILogger<InternalWarehouseController> log)
    {
        _tenant = tenant;
        _services = services;
        _log = log;
    }

    [HttpPost("site")]
    public async Task<IActionResult> CreateSiteWarehouse(
        [FromBody] CreateSiteWarehouseRequest request,
        CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new { Message = "CustomerId and OrgId are required." });
        }

        if (request.SiteId <= 0)
        {
            return BadRequest(new { Message = "SiteId is required." });
        }

        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var db = _services.GetRequiredService<InventoryDbContext>();

        var existing = await db.Warehouses
            .FirstOrDefaultAsync(w => w.SiteId == request.SiteId, ct);

        if (existing is not null)
        {
            return Ok(new CreateSiteWarehouseResponse
            {
                WarehouseId = existing.WarehouseId,
                WarehouseCode = existing.WarehouseCode,
            });
        }

        string code = $"WH-SIT-{request.SiteCode}".Trim();
        if (code.Length > 20)
        {
            code = code[..20];
        }

        int counter = 1;
        string baseCode = code;
        while (await db.Warehouses.AnyAsync(w => w.WarehouseCode == code, ct))
        {
            string suffix = $"-{counter++}";
            code = baseCode.Length + suffix.Length > 20
                ? baseCode[..(20 - suffix.Length)] + suffix
                : baseCode + suffix;
        }

        string name = $"Site: {request.SiteName}".Trim();
        if (name.Length > 100)
        {
            name = name[..100];
        }

        var warehouse = new Warehouse
        {
            WarehouseCode = code,
            WarehouseName = name,
            WarehouseType = WarehouseType.Site,
            SiteId = request.SiteId,
            IsActive = true,
            IsDefault = false,
        };

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);

        _log.LogInformation("Created site warehouse {WarehouseId} ({Code}) for site {SiteId}",
            warehouse.WarehouseId, warehouse.WarehouseCode, request.SiteId);

        return Ok(new CreateSiteWarehouseResponse
        {
            WarehouseId = warehouse.WarehouseId,
            WarehouseCode = warehouse.WarehouseCode,
        });
    }
}

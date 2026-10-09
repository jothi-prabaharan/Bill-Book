using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Site.Entity.Enums;
using Site.Entity.TableEntities;
using Site.Repository.Clients;
using Site.Repository.Repositories;

namespace Site.Api.Controllers;

public sealed class SaveSiteRequest
{
    public string? SiteCode { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public long ContactId { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 200;
    public SiteStatus Status { get; set; } = SiteStatus.Planning;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EstimatedEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
}

[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/site/sites")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class SitesController : ControllerBase
{
    private readonly SiteRepository _repo;
    private readonly INumberGenerator _numbers;
    private readonly IInventoryWarehouseClient _warehouseClient;
    private readonly ITenantContext _tenant;

    public SitesController(
        SiteRepository repo,
        INumberGenerator numbers,
        IInventoryWarehouseClient warehouseClient,
        ITenantContext tenant)
    {
        _repo = repo;
        _numbers = numbers;
        _warehouseClient = warehouseClient;
        _tenant = tenant;
    }

    [HttpGet]
    [RequirePermission("site.view")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _repo.ListAsync(ct));

    [HttpGet("{siteId:long}")]
    [RequirePermission("site.view")]
    public async Task<IActionResult> Get(long siteId, CancellationToken ct)
    {
        SiteJob? site = await _repo.GetAsync(siteId, ct);
        return site is null ? NotFound() : Ok(site);
    }

    [HttpPost]
    [RequirePermission("site.edit")]
    public async Task<IActionResult> Create([FromBody] SaveSiteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.SiteName) || req.ContactId <= 0)
        {
            return BadRequest(new { Message = "SiteName and ContactId are required." });
        }

        string code = req.SiteCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            DateOnly effectiveDate = req.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var alloc = await _numbers.NextAsync("SIT", effectiveDate, ct);
            code = alloc.Code;
        }

        var site = new SiteJob
        {
            SiteCode = code,
            SiteName = req.SiteName.Trim(),
            ContactId = req.ContactId,
            Address = req.Address.Trim(),
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            GeofenceRadiusMeters = req.GeofenceRadiusMeters > 0 ? req.GeofenceRadiusMeters : 200,
            Status = req.Status,
            StartDate = req.StartDate,
            EstimatedEndDate = req.EstimatedEndDate,
            ActualEndDate = req.ActualEndDate,
        };

        await _repo.CreateSiteAsync(site, ct);

        // Auto-create site warehouse in Inventory (TK-170)
        long? warehouseId = await _warehouseClient.CreateSiteWarehouseAsync(
            _tenant.CustomerId ?? Guid.Empty,
            _tenant.OrgId ?? Guid.Empty,
            site.SiteId,
            site.SiteCode,
            site.SiteName,
            ct);

        if (warehouseId.HasValue)
        {
            site.WarehouseId = warehouseId.Value;
            await _repo.UpdateSiteAsync(site, ct);
        }

        return Ok(site);
    }

    [HttpPut("{siteId:long}")]
    [RequirePermission("site.edit")]
    public async Task<IActionResult> Update(long siteId, [FromBody] SaveSiteRequest req, CancellationToken ct)
    {
        var site = new SiteJob
        {
            SiteId = siteId,
            SiteName = req.SiteName.Trim(),
            ContactId = req.ContactId,
            Address = req.Address.Trim(),
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            GeofenceRadiusMeters = req.GeofenceRadiusMeters > 0 ? req.GeofenceRadiusMeters : 200,
            Status = req.Status,
            StartDate = req.StartDate,
            EstimatedEndDate = req.EstimatedEndDate,
            ActualEndDate = req.ActualEndDate,
        };

        var updated = await _repo.UpdateSiteAsync(site, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}

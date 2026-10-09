using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Site.Entity.Enums;
using Site.Entity.TableEntities;
using Site.Repository.Repositories;

namespace Site.Api.Controllers;

public sealed class SaveLaborerRequest
{
    public long TradeId { get; set; }
    public Guid? UserId { get; set; }
    public long? EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal? CostRatePerDay { get; set; }
    public decimal? SellRatePerDay { get; set; }
    public decimal? OtCostRatePerHour { get; set; }
    public decimal? OtSellRatePerHour { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class LaborerDto
{
    public long LaborerId { get; set; }
    public long TradeId { get; set; }
    public Guid? UserId { get; set; }
    public long? EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal? CostRatePerDay { get; set; }
    public decimal? SellRatePerDay { get; set; }
    public decimal? OtCostRatePerHour { get; set; }
    public decimal? OtSellRatePerHour { get; set; }
    public bool IsActive { get; set; }
}

[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/site/laborers")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class LaborersController : ControllerBase
{
    private readonly SiteRepository _repo;

    public LaborersController(SiteRepository repo) => _repo = repo;

    private bool CanViewCost =>
        User.FindAll("permission").Any(c => string.Equals(c.Value, "site.cost.view", StringComparison.OrdinalIgnoreCase));

    private LaborerDto ToDto(Laborer l) => new()
    {
        LaborerId = l.LaborerId,
        TradeId = l.TradeId,
        UserId = l.UserId,
        EmployeeId = l.EmployeeId,
        FullName = l.FullName,
        Phone = l.Phone,
        CostRatePerDay = CanViewCost ? l.CostRatePerDay : null,
        SellRatePerDay = l.SellRatePerDay,
        OtCostRatePerHour = CanViewCost ? l.OtCostRatePerHour : null,
        OtSellRatePerHour = l.OtSellRatePerHour,
        IsActive = l.IsActive,
    };

    [HttpGet]
    [RequirePermission("site.view")]
    public async Task<IActionResult> List([FromQuery] long? tradeId, CancellationToken ct)
    {
        var laborers = await _repo.ListLaborersAsync(tradeId, ct);
        return Ok(laborers.Select(ToDto));
    }

    [HttpGet("{laborerId:long}")]
    [RequirePermission("site.view")]
    public async Task<IActionResult> Get(long laborerId, CancellationToken ct)
    {
        Laborer? laborer = await _repo.GetLaborerAsync(laborerId, ct);
        return laborer is null ? NotFound() : Ok(ToDto(laborer));
    }

    [HttpPost]
    [RequirePermission("site.edit")]
    public async Task<IActionResult> Create([FromBody] SaveLaborerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Phone) || req.TradeId <= 0)
        {
            return BadRequest(new { Message = "FullName, Phone, and TradeId are required." });
        }

        var laborer = new Laborer
        {
            TradeId = req.TradeId,
            UserId = req.UserId,
            EmployeeId = req.EmployeeId,
            FullName = req.FullName.Trim(),
            Phone = req.Phone.Trim(),
            CostRatePerDay = req.CostRatePerDay,
            SellRatePerDay = req.SellRatePerDay,
            OtCostRatePerHour = req.OtCostRatePerHour,
            OtSellRatePerHour = req.OtSellRatePerHour,
            IsActive = req.IsActive,
        };

        await _repo.CreateLaborerAsync(laborer, ct);
        return Ok(ToDto(laborer));
    }

    [HttpPut("{laborerId:long}")]
    [RequirePermission("site.edit")]
    public async Task<IActionResult> Update(long laborerId, [FromBody] SaveLaborerRequest req, CancellationToken ct)
    {
        var laborer = new Laborer
        {
            LaborerId = laborerId,
            TradeId = req.TradeId,
            UserId = req.UserId,
            EmployeeId = req.EmployeeId,
            FullName = req.FullName.Trim(),
            Phone = req.Phone.Trim(),
            CostRatePerDay = req.CostRatePerDay,
            SellRatePerDay = req.SellRatePerDay,
            OtCostRatePerHour = req.OtCostRatePerHour,
            OtSellRatePerHour = req.OtSellRatePerHour,
            IsActive = req.IsActive,
        };

        var updated = await _repo.UpdateLaborerAsync(laborer, ct);
        return updated is null ? NotFound() : Ok(ToDto(updated));
    }
}

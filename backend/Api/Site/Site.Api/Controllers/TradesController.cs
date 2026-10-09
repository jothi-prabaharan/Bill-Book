using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Site.Entity.TableEntities;
using Site.Repository.Repositories;

namespace Site.Api.Controllers;

public sealed class SaveTradeRequest
{
    public string TradeCode { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public long? DefaultServiceItemId { get; set; }
    public decimal DefaultCostRatePerDay { get; set; }
    public decimal DefaultSellRatePerDay { get; set; }
    public decimal DefaultOtCostRatePerHour { get; set; }
    public decimal DefaultOtSellRatePerHour { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class TradeDto
{
    public long TradeId { get; set; }
    public string TradeCode { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public long? DefaultServiceItemId { get; set; }
    public decimal? DefaultCostRatePerDay { get; set; }
    public decimal DefaultSellRatePerDay { get; set; }
    public decimal? DefaultOtCostRatePerHour { get; set; }
    public decimal DefaultOtSellRatePerHour { get; set; }
    public bool IsActive { get; set; }
}

[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/site/trades")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class TradesController : ControllerBase
{
    private readonly SiteRepository _repo;

    public TradesController(SiteRepository repo) => _repo = repo;

    private bool CanViewCost =>
        User.FindAll("permission").Any(c => string.Equals(c.Value, "site.cost.view", StringComparison.OrdinalIgnoreCase));

    private TradeDto ToDto(Trade t) => new()
    {
        TradeId = t.TradeId,
        TradeCode = t.TradeCode,
        TradeName = t.TradeName,
        DefaultServiceItemId = t.DefaultServiceItemId,
        DefaultCostRatePerDay = CanViewCost ? t.DefaultCostRatePerDay : null,
        DefaultSellRatePerDay = t.DefaultSellRatePerDay,
        DefaultOtCostRatePerHour = CanViewCost ? t.DefaultOtCostRatePerHour : null,
        DefaultOtSellRatePerHour = t.DefaultOtSellRatePerHour,
        IsActive = t.IsActive,
    };

    [HttpGet]
    [RequirePermission("site.view")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var trades = await _repo.ListTradesAsync(ct);
        return Ok(trades.Select(ToDto));
    }

    [HttpGet("{tradeId:long}")]
    [RequirePermission("site.view")]
    public async Task<IActionResult> Get(long tradeId, CancellationToken ct)
    {
        Trade? trade = await _repo.GetTradeAsync(tradeId, ct);
        return trade is null ? NotFound() : Ok(ToDto(trade));
    }

    [HttpPost]
    [RequirePermission("site.edit")]
    public async Task<IActionResult> Create([FromBody] SaveTradeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TradeCode) || string.IsNullOrWhiteSpace(req.TradeName))
        {
            return BadRequest(new { Message = "TradeCode and TradeName are required." });
        }

        var trade = new Trade
        {
            TradeCode = req.TradeCode.Trim().ToUpperInvariant(),
            TradeName = req.TradeName.Trim(),
            DefaultServiceItemId = req.DefaultServiceItemId,
            DefaultCostRatePerDay = req.DefaultCostRatePerDay,
            DefaultSellRatePerDay = req.DefaultSellRatePerDay,
            DefaultOtCostRatePerHour = req.DefaultOtCostRatePerHour,
            DefaultOtSellRatePerHour = req.DefaultOtSellRatePerHour,
            IsActive = req.IsActive,
        };

        await _repo.CreateTradeAsync(trade, ct);
        return Ok(ToDto(trade));
    }

    [HttpPut("{tradeId:long}")]
    [RequirePermission("site.edit")]
    public async Task<IActionResult> Update(long tradeId, [FromBody] SaveTradeRequest req, CancellationToken ct)
    {
        var trade = new Trade
        {
            TradeId = tradeId,
            TradeName = req.TradeName.Trim(),
            DefaultServiceItemId = req.DefaultServiceItemId,
            DefaultCostRatePerDay = req.DefaultCostRatePerDay,
            DefaultSellRatePerDay = req.DefaultSellRatePerDay,
            DefaultOtCostRatePerHour = req.DefaultOtCostRatePerHour,
            DefaultOtSellRatePerHour = req.DefaultOtSellRatePerHour,
            IsActive = req.IsActive,
        };

        var updated = await _repo.UpdateTradeAsync(trade, ct);
        return updated is null ? NotFound() : Ok(ToDto(updated));
    }
}

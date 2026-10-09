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

public sealed class SaveMarginRuleRequest
{
    public MarginScope Scope { get; set; } = MarginScope.Branch;
    public long? ScopeId { get; set; }
    public MarginAppliesTo AppliesTo { get; set; } = MarginAppliesTo.All;
    public MarginType MarginType { get; set; } = MarginType.Percent;
    public decimal MarginValue { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class MarginRuleDto
{
    public long MarginRuleId { get; set; }
    public MarginScope Scope { get; set; }
    public long? ScopeId { get; set; }
    public MarginAppliesTo AppliesTo { get; set; }
    public MarginType MarginType { get; set; }
    public decimal? MarginValue { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ResolveMarginRequest
{
    public long? AssignmentId { get; set; }
    public long? SiteId { get; set; }
    public long? TradeId { get; set; }
    public long? ItemId { get; set; }
    public MarginAppliesTo AppliesTo { get; set; } = MarginAppliesTo.Labor;
    public DateOnly? Date { get; set; }
    public decimal Cost { get; set; }
}

public sealed class ResolveMarginResponse
{
    public MarginRuleDto? Rule { get; set; }
    public decimal? MarginAmount { get; set; }
    public decimal SellingPrice { get; set; }
}

[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/site/margin-rules")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class MarginRulesController : ControllerBase
{
    private readonly SiteRepository _repo;
    private readonly SiteMarginRepository _marginRepo;

    public MarginRulesController(SiteRepository repo, SiteMarginRepository marginRepo)
    {
        _repo = repo;
        _marginRepo = marginRepo;
    }

    private bool CanViewCost =>
        User.FindAll("permission").Any(c => string.Equals(c.Value, "site.cost.view", StringComparison.OrdinalIgnoreCase));

    private MarginRuleDto ToDto(MarginRule r) => new()
    {
        MarginRuleId = r.MarginRuleId,
        Scope = r.Scope,
        ScopeId = r.ScopeId,
        AppliesTo = r.AppliesTo,
        MarginType = r.MarginType,
        MarginValue = CanViewCost ? r.MarginValue : null,
        EffectiveFrom = r.EffectiveFrom,
        EffectiveTo = r.EffectiveTo,
        IsActive = r.IsActive,
    };

    [HttpGet]
    [RequirePermission("site.view")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rules = await _repo.ListMarginRulesAsync(ct);
        return Ok(rules.Select(ToDto));
    }

    [HttpGet("{ruleId:long}")]
    [RequirePermission("site.view")]
    public async Task<IActionResult> Get(long ruleId, CancellationToken ct)
    {
        MarginRule? rule = await _repo.GetMarginRuleAsync(ruleId, ct);
        return rule is null ? NotFound() : Ok(ToDto(rule));
    }

    [HttpPost]
    [RequirePermission("site.edit")]
    public async Task<IActionResult> Create([FromBody] SaveMarginRuleRequest req, CancellationToken ct)
    {
        var rule = new MarginRule
        {
            Scope = req.Scope,
            ScopeId = req.ScopeId,
            AppliesTo = req.AppliesTo,
            MarginType = req.MarginType,
            MarginValue = req.MarginValue,
            EffectiveFrom = req.EffectiveFrom,
            EffectiveTo = req.EffectiveTo,
            IsActive = req.IsActive,
        };

        await _repo.CreateMarginRuleAsync(rule, ct);
        return Ok(ToDto(rule));
    }

    [HttpPut("{ruleId:long}")]
    [RequirePermission("site.edit")]
    public async Task<IActionResult> Update(long ruleId, [FromBody] SaveMarginRuleRequest req, CancellationToken ct)
    {
        var rule = new MarginRule
        {
            MarginRuleId = ruleId,
            Scope = req.Scope,
            ScopeId = req.ScopeId,
            AppliesTo = req.AppliesTo,
            MarginType = req.MarginType,
            MarginValue = req.MarginValue,
            EffectiveFrom = req.EffectiveFrom,
            EffectiveTo = req.EffectiveTo,
            IsActive = req.IsActive,
        };

        var updated = await _repo.UpdateMarginRuleAsync(rule, ct);
        return updated is null ? NotFound() : Ok(ToDto(updated));
    }

    [HttpPost("resolve")]
    [RequirePermission("site.view")]
    public async Task<IActionResult> Resolve([FromBody] ResolveMarginRequest req, CancellationToken ct)
    {
        DateOnly date = req.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var rule = await _marginRepo.ResolveRuleAsync(
            req.AssignmentId,
            req.SiteId,
            req.TradeId,
            req.ItemId,
            req.AppliesTo,
            date,
            ct);

        var calc = SiteMarginRepository.Calculate(rule, req.Cost);

        return Ok(new ResolveMarginResponse
        {
            Rule = rule is null ? null : ToDto(rule),
            MarginAmount = CanViewCost ? calc.MarginAmount : null,
            SellingPrice = calc.SellingPrice,
        });
    }
}

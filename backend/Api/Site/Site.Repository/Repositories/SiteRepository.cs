using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Site.Entity.TableEntities;

namespace Site.Repository.Repositories;

public sealed class SiteRepository
{
    private readonly SiteDbContext _db;

    public SiteRepository(SiteDbContext db) => _db = db;

    // --- Sites ---
    public async Task<IReadOnlyList<SiteJob>> ListAsync(CancellationToken ct = default) =>
        await _db.Sites.AsNoTracking().OrderBy(s => s.SiteName).ToListAsync(ct);

    public async Task<SiteJob?> GetAsync(long siteId, CancellationToken ct = default) =>
        await _db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == siteId, ct);

    public async Task<SiteJob> CreateSiteAsync(SiteJob site, CancellationToken ct = default)
    {
        _db.Sites.Add(site);
        await _db.SaveChangesAsync(ct);
        return site;
    }

    public async Task<SiteJob?> UpdateSiteAsync(SiteJob site, CancellationToken ct = default)
    {
        var existing = await _db.Sites.FirstOrDefaultAsync(s => s.SiteId == site.SiteId, ct);
        if (existing is null) return null;

        existing.SiteName = site.SiteName;
        existing.ContactId = site.ContactId;
        existing.Address = site.Address;
        existing.Latitude = site.Latitude;
        existing.Longitude = site.Longitude;
        existing.GeofenceRadiusMeters = site.GeofenceRadiusMeters;
        existing.Status = site.Status;
        existing.StartDate = site.StartDate;
        existing.EstimatedEndDate = site.EstimatedEndDate;
        existing.ActualEndDate = site.ActualEndDate;
        if (site.WarehouseId.HasValue) existing.WarehouseId = site.WarehouseId;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    // --- Trades ---
    public async Task<IReadOnlyList<Trade>> ListTradesAsync(CancellationToken ct = default) =>
        await _db.Trades.AsNoTracking().OrderBy(t => t.TradeName).ToListAsync(ct);

    public async Task<Trade?> GetTradeAsync(long tradeId, CancellationToken ct = default) =>
        await _db.Trades.AsNoTracking().FirstOrDefaultAsync(t => t.TradeId == tradeId, ct);

    public async Task<Trade> CreateTradeAsync(Trade trade, CancellationToken ct = default)
    {
        _db.Trades.Add(trade);
        await _db.SaveChangesAsync(ct);
        return trade;
    }

    public async Task<Trade?> UpdateTradeAsync(Trade trade, CancellationToken ct = default)
    {
        var existing = await _db.Trades.FirstOrDefaultAsync(t => t.TradeId == trade.TradeId, ct);
        if (existing is null) return null;

        existing.TradeName = trade.TradeName;
        existing.DefaultServiceItemId = trade.DefaultServiceItemId;
        existing.DefaultCostRatePerDay = trade.DefaultCostRatePerDay;
        existing.DefaultSellRatePerDay = trade.DefaultSellRatePerDay;
        existing.DefaultOtCostRatePerHour = trade.DefaultOtCostRatePerHour;
        existing.DefaultOtSellRatePerHour = trade.DefaultOtSellRatePerHour;
        existing.IsActive = trade.IsActive;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    // --- Laborers ---
    public async Task<IReadOnlyList<Laborer>> ListLaborersAsync(long? tradeId = null, CancellationToken ct = default)
    {
        var query = _db.Laborers.AsNoTracking();
        if (tradeId.HasValue)
            query = query.Where(l => l.TradeId == tradeId.Value);

        return await query.OrderBy(l => l.FullName).ToListAsync(ct);
    }

    public async Task<Laborer?> GetLaborerAsync(long laborerId, CancellationToken ct = default) =>
        await _db.Laborers.AsNoTracking().FirstOrDefaultAsync(l => l.LaborerId == laborerId, ct);

    public async Task<Laborer> CreateLaborerAsync(Laborer laborer, CancellationToken ct = default)
    {
        _db.Laborers.Add(laborer);
        await _db.SaveChangesAsync(ct);
        return laborer;
    }

    public async Task<Laborer?> UpdateLaborerAsync(Laborer laborer, CancellationToken ct = default)
    {
        var existing = await _db.Laborers.FirstOrDefaultAsync(l => l.LaborerId == laborer.LaborerId, ct);
        if (existing is null) return null;

        existing.TradeId = laborer.TradeId;
        existing.UserId = laborer.UserId;
        existing.EmployeeId = laborer.EmployeeId;
        existing.FullName = laborer.FullName;
        existing.Phone = laborer.Phone;
        existing.CostRatePerDay = laborer.CostRatePerDay;
        existing.SellRatePerDay = laborer.SellRatePerDay;
        existing.OtCostRatePerHour = laborer.OtCostRatePerHour;
        existing.OtSellRatePerHour = laborer.OtSellRatePerHour;
        existing.IsActive = laborer.IsActive;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    // --- Site Assignments ---
    public async Task<IReadOnlyList<SiteAssignment>> ListAssignmentsAsync(
        long? siteId = null,
        long? laborerId = null,
        CancellationToken ct = default)
    {
        var query = _db.SiteAssignments.AsNoTracking();
        if (siteId.HasValue)
            query = query.Where(a => a.SiteId == siteId.Value);
        if (laborerId.HasValue)
            query = query.Where(a => a.LaborerId == laborerId.Value);

        return await query.OrderByDescending(a => a.StartDate).ToListAsync(ct);
    }

    public async Task<SiteAssignment?> GetAssignmentAsync(long assignmentId, CancellationToken ct = default) =>
        await _db.SiteAssignments.AsNoTracking().FirstOrDefaultAsync(a => a.SiteAssignmentId == assignmentId, ct);

    public async Task<SiteAssignment> CreateAssignmentAsync(SiteAssignment assignment, CancellationToken ct = default)
    {
        _db.SiteAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task<SiteAssignment?> UpdateAssignmentAsync(SiteAssignment assignment, CancellationToken ct = default)
    {
        var existing = await _db.SiteAssignments.FirstOrDefaultAsync(a => a.SiteAssignmentId == assignment.SiteAssignmentId, ct);
        if (existing is null) return null;

        existing.StartDate = assignment.StartDate;
        existing.EndDate = assignment.EndDate;
        existing.CostRatePerDay = assignment.CostRatePerDay;
        existing.SellRatePerDay = assignment.SellRatePerDay;
        existing.Status = assignment.Status;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    // --- Margin Rules ---
    public async Task<IReadOnlyList<MarginRule>> ListMarginRulesAsync(CancellationToken ct = default) =>
        await _db.MarginRules.AsNoTracking().OrderBy(m => m.Scope).ThenBy(m => m.EffectiveFrom).ToListAsync(ct);

    public async Task<MarginRule?> GetMarginRuleAsync(long ruleId, CancellationToken ct = default) =>
        await _db.MarginRules.AsNoTracking().FirstOrDefaultAsync(m => m.MarginRuleId == ruleId, ct);

    public async Task<MarginRule> CreateMarginRuleAsync(MarginRule rule, CancellationToken ct = default)
    {
        _db.MarginRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<MarginRule?> UpdateMarginRuleAsync(MarginRule rule, CancellationToken ct = default)
    {
        var existing = await _db.MarginRules.FirstOrDefaultAsync(m => m.MarginRuleId == rule.MarginRuleId, ct);
        if (existing is null) return null;

        existing.Scope = rule.Scope;
        existing.ScopeId = rule.ScopeId;
        existing.AppliesTo = rule.AppliesTo;
        existing.MarginType = rule.MarginType;
        existing.MarginValue = rule.MarginValue;
        existing.EffectiveFrom = rule.EffectiveFrom;
        existing.EffectiveTo = rule.EffectiveTo;
        existing.IsActive = rule.IsActive;

        await _db.SaveChangesAsync(ct);
        return existing;
    }
}

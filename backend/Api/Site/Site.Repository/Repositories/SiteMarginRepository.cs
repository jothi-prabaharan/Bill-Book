using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Site.Entity.Enums;
using Site.Entity.TableEntities;

namespace Site.Repository.Repositories;

public sealed class MarginResolutionResult
{
    public MarginRule? Rule { get; init; }
    public decimal MarginAmount { get; init; }
    public decimal SellingPrice { get; init; }
}

public sealed class ResolvedRates
{
    public decimal CostRatePerDay { get; init; }
    public decimal SellRatePerDay { get; init; }
    public decimal OtCostRatePerHour { get; init; }
    public decimal OtSellRatePerHour { get; init; }
}

/// <summary>
/// Resolves tiered margin rules and cost-rate fallbacks (TK-170).
/// Hierarchy: Assignment -> Site -> Trade/Item -> Branch.
/// Cost-rate fallback: Assignment -> Laborer -> Trade default.
/// </summary>
public class SiteMarginRepository
{
    private readonly SiteDbContext _db;

    public SiteMarginRepository(SiteDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Resolves the effective margin rule following the hierarchy:
    /// Assignment -> Site -> Trade or Item -> Branch.
    /// </summary>
    public async Task<MarginRule?> ResolveRuleAsync(
        long? assignmentId,
        long? siteId,
        long? tradeId,
        long? itemId,
        MarginAppliesTo appliesTo,
        DateOnly date,
        CancellationToken ct = default)
    {
        var rules = await _db.MarginRules
            .Where(r => r.IsActive
                && (r.AppliesTo == MarginAppliesTo.All || r.AppliesTo == appliesTo)
                && r.EffectiveFrom <= date
                && (r.EffectiveTo == null || r.EffectiveTo >= date))
            .ToListAsync(ct);

        return ResolveRuleFromList(rules, assignmentId, siteId, tradeId, itemId, appliesTo);
    }

    /// <summary>
    /// Pure resolution logic over a collection of candidate rules.
    /// </summary>
    public static MarginRule? ResolveRuleFromList(
        IEnumerable<MarginRule> candidateRules,
        long? assignmentId,
        long? siteId,
        long? tradeId,
        long? itemId,
        MarginAppliesTo appliesTo)
    {
        var rules = candidateRules.ToList();

        // 1. Assignment scope
        if (assignmentId.HasValue)
        {
            var match = PickBest(rules.Where(r => r.Scope == MarginScope.Assignment && r.ScopeId == assignmentId.Value), appliesTo);
            if (match is not null) return match;
        }

        // 2. Site scope
        if (siteId.HasValue)
        {
            var match = PickBest(rules.Where(r => r.Scope == MarginScope.Site && r.ScopeId == siteId.Value), appliesTo);
            if (match is not null) return match;
        }

        // 3. Trade or Item scope
        if (appliesTo == MarginAppliesTo.Material && itemId.HasValue)
        {
            var match = PickBest(rules.Where(r => r.Scope == MarginScope.Item && r.ScopeId == itemId.Value), appliesTo);
            if (match is not null) return match;
        }
        else if (tradeId.HasValue)
        {
            var match = PickBest(rules.Where(r => r.Scope == MarginScope.Trade && r.ScopeId == tradeId.Value), appliesTo);
            if (match is not null) return match;
        }

        // 4. Branch default scope
        return PickBest(rules.Where(r => r.Scope == MarginScope.Branch), appliesTo);
    }

    private static MarginRule? PickBest(IEnumerable<MarginRule> matching, MarginAppliesTo appliesTo)
    {
        return matching
            .OrderByDescending(r => r.AppliesTo == appliesTo ? 1 : 0) // Exact appliesTo preferred over All
            .ThenByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();
    }

    /// <summary>
    /// Calculates the margin amount and total selling price from cost and rule.
    /// FlatPerUnit adds the value; Percent adds cost * value / 100.
    /// </summary>
    public static MarginResolutionResult Calculate(MarginRule? rule, decimal cost)
    {
        if (rule is null)
        {
            return new MarginResolutionResult
            {
                Rule = null,
                MarginAmount = 0m,
                SellingPrice = cost,
            };
        }

        decimal margin = rule.MarginType switch
        {
            MarginType.FlatPerUnit => rule.MarginValue,
            MarginType.Percent => Math.Round(cost * (rule.MarginValue / 100m), 4),
            _ => 0m,
        };

        return new MarginResolutionResult
        {
            Rule = rule,
            MarginAmount = margin,
            SellingPrice = cost + margin,
        };
    }

    /// <summary>
    /// Cost-rate fallback: assignment -> laborer -> trade default.
    /// </summary>
    public static ResolvedRates ResolveRates(
        SiteAssignment? assignment,
        Laborer? laborer,
        Trade? trade)
    {
        decimal costPerDay = assignment?.CostRatePerDay
            ?? laborer?.CostRatePerDay
            ?? trade?.DefaultCostRatePerDay
            ?? 0m;

        decimal sellPerDay = assignment?.SellRatePerDay
            ?? laborer?.SellRatePerDay
            ?? trade?.DefaultSellRatePerDay
            ?? 0m;

        decimal otCostPerHour = laborer?.OtCostRatePerHour
            ?? trade?.DefaultOtCostRatePerHour
            ?? 0m;

        decimal otSellPerHour = laborer?.OtSellRatePerHour
            ?? trade?.DefaultOtSellRatePerHour
            ?? 0m;

        return new ResolvedRates
        {
            CostRatePerDay = costPerDay,
            SellRatePerDay = sellPerDay,
            OtCostRatePerHour = otCostPerHour,
            OtSellRatePerHour = otSellPerHour,
        };
    }
}

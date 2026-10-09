using System;
using System.Collections.Generic;
using Site.Entity.Enums;
using Site.Entity.TableEntities;
using Site.Repository.Repositories;
using Xunit;

namespace Site.Api.Tests;

public sealed class SiteMarginRepositoryTests
{
    [Fact]
    public void Margin_rule_resolves_site_override_over_trade_and_trade_over_branch()
    {
        // TK-170: a ₹1000 flat day margin on Electrical and a 10% margin on one site
        // resolve to the site's 10% for a laborer there and to ₹1000 elsewhere.
        var today = new DateOnly(2026, 10, 9);
        long electricalTradeId = 101;
        long siteAId = 201;
        long siteBId = 202;

        var tradeRule = new MarginRule
        {
            MarginRuleId = 1,
            Scope = MarginScope.Trade,
            ScopeId = electricalTradeId,
            AppliesTo = MarginAppliesTo.Labor,
            MarginType = MarginType.FlatPerUnit,
            MarginValue = 1000m,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            IsActive = true,
        };

        var siteARule = new MarginRule
        {
            MarginRuleId = 2,
            Scope = MarginScope.Site,
            ScopeId = siteAId,
            AppliesTo = MarginAppliesTo.Labor,
            MarginType = MarginType.Percent,
            MarginValue = 10m,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            IsActive = true,
        };

        var rules = new List<MarginRule> { tradeRule, siteARule };

        // 1. Laborer working on Site A: resolves to Site A's 10% rule
        var resolvedForSiteA = SiteMarginRepository.ResolveRuleFromList(
            rules,
            assignmentId: null,
            siteId: siteAId,
            tradeId: electricalTradeId,
            itemId: null,
            MarginAppliesTo.Labor);

        Assert.NotNull(resolvedForSiteA);
        Assert.Equal(siteARule.MarginRuleId, resolvedForSiteA!.MarginRuleId);
        Assert.Equal(MarginType.Percent, resolvedForSiteA.MarginType);

        var calcA = SiteMarginRepository.Calculate(resolvedForSiteA, 5000m);
        Assert.Equal(500m, calcA.MarginAmount);
        Assert.Equal(5500m, calcA.SellingPrice);

        // 2. Laborer working on Site B: resolves to Electrical trade's ₹1000 flat rule
        var resolvedForSiteB = SiteMarginRepository.ResolveRuleFromList(
            rules,
            assignmentId: null,
            siteId: siteBId,
            tradeId: electricalTradeId,
            itemId: null,
            MarginAppliesTo.Labor);

        Assert.NotNull(resolvedForSiteB);
        Assert.Equal(tradeRule.MarginRuleId, resolvedForSiteB!.MarginRuleId);
        Assert.Equal(MarginType.FlatPerUnit, resolvedForSiteB.MarginType);

        var calcB = SiteMarginRepository.Calculate(resolvedForSiteB, 5000m);
        Assert.Equal(1000m, calcB.MarginAmount);
        Assert.Equal(6000m, calcB.SellingPrice);
    }

    [Fact]
    public void Cost_rate_fallback_chain_checks_assignment_then_laborer_then_trade()
    {
        var trade = new Trade
        {
            TradeId = 1,
            DefaultCostRatePerDay = 1000m,
            DefaultSellRatePerDay = 1500m,
            DefaultOtCostRatePerHour = 150m,
            DefaultOtSellRatePerHour = 220m,
        };

        var laborer = new Laborer
        {
            LaborerId = 10,
            TradeId = 1,
            CostRatePerDay = 1200m,
            SellRatePerDay = 1800m,
            OtCostRatePerHour = 180m,
            OtSellRatePerHour = 260m,
        };

        var assignmentWithOverride = new SiteAssignment
        {
            SiteAssignmentId = 100,
            CostRatePerDay = 1400m,
            SellRatePerDay = 2100m,
        };

        var assignmentWithoutOverride = new SiteAssignment
        {
            SiteAssignmentId = 101,
            CostRatePerDay = null,
            SellRatePerDay = null,
        };

        // 1. With assignment override -> uses assignment rate
        var rates1 = SiteMarginRepository.ResolveRates(assignmentWithOverride, laborer, trade);
        Assert.Equal(1400m, rates1.CostRatePerDay);
        Assert.Equal(2100m, rates1.SellRatePerDay);
        Assert.Equal(180m, rates1.OtCostRatePerHour); // from laborer
        Assert.Equal(260m, rates1.OtSellRatePerHour); // from laborer

        // 2. Without assignment override -> falls back to laborer rate
        var rates2 = SiteMarginRepository.ResolveRates(assignmentWithoutOverride, laborer, trade);
        Assert.Equal(1200m, rates2.CostRatePerDay);
        Assert.Equal(1800m, rates2.SellRatePerDay);

        // 3. Laborer without rate -> falls back to trade default
        var laborerWithoutRate = new Laborer { LaborerId = 11, TradeId = 1 };
        var rates3 = SiteMarginRepository.ResolveRates(assignmentWithoutOverride, laborerWithoutRate, trade);
        Assert.Equal(1000m, rates3.CostRatePerDay);
        Assert.Equal(1500m, rates3.SellRatePerDay);
        Assert.Equal(150m, rates3.OtCostRatePerHour);
        Assert.Equal(220m, rates3.OtSellRatePerHour);
    }
}

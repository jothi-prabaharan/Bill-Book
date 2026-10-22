using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Master.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// What the paid plans cost (D-71, TK-211): one seeded row for every trade, plan
/// and billing cycle, Elite at three times Professional, read by the Plan screen
/// and editable by an operator without the next start putting the figure back.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class PlanPriceTests
{
    private readonly AdminFixture _admin;

    public PlanPriceTests(AdminFixture admin) => _admin = admin;

    // ---- The seed rule, pure ----------------------------------------------

    [Theory]
    [InlineData(1500, BillingCycle.Quarterly, 4050)]
    [InlineData(1500, BillingCycle.BiAnnual, 7650)]
    [InlineData(2200, BillingCycle.Quarterly, 5940)]
    [InlineData(4000, BillingCycle.BiAnnual, 20400)]
    [InlineData(5000, BillingCycle.Quarterly, 13500)]
    public void PRICE_01_a_cycle_costs_its_months_less_the_discount_for_paying_ahead(int monthly, BillingCycle cycle, int expected) =>
        Assert.Equal((decimal)expected, PlanPriceSeed.Professional(monthly, cycle));

    [Fact]
    public void PRICE_02_a_cycle_is_a_quarter_or_half_a_year_and_nothing_else_is_a_cycle()
    {
        Assert.Equal(3, PlanPriceSeed.Months(BillingCycle.Quarterly));
        Assert.Equal(6, PlanPriceSeed.Months(BillingCycle.BiAnnual));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanPriceSeed.Months((BillingCycle)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanPriceSeed.Discount((BillingCycle)99));
    }

    [Fact]
    public void PRICE_03_every_trade_has_a_professional_and_an_elite_price_in_each_cycle()
    {
        IReadOnlyList<PlanPrice> rows = PlanPriceSeed.Build();

        Assert.Equal(Enum.GetValues<BusinessType>().Length * Enum.GetValues<BillingCycle>().Length * 2, rows.Count);
        Assert.Equal(rows.Count, rows.Select(r => (r.BusinessType, r.Plan, r.BillingCycle)).Distinct().Count());
        Assert.All(rows, r =>
        {
            Assert.True(r.IsActive);
            Assert.Equal("INR", r.CurrencyCode);
            Assert.True(r.Plan is LicenseType.Pro or LicenseType.Elite);
        });
    }

    [Fact]
    public void PRICE_04_elite_costs_three_times_professional_for_the_same_trade_and_cycle()
    {
        foreach (var pair in PlanPriceSeed.Build().GroupBy(r => (r.BusinessType, r.BillingCycle)))
        {
            decimal professional = pair.Single(r => r.Plan == LicenseType.Pro).Amount;
            decimal elite = pair.Single(r => r.Plan == LicenseType.Elite).Amount;

            Assert.Equal(professional * PlanPriceSeed.EliteMultiple, elite);
        }

        Assert.Equal(3m, PlanPriceSeed.EliteMultiple);
    }

    [Fact]
    public void PRICE_05_every_trade_the_product_has_is_priced_so_a_new_one_cannot_go_unpriced() =>
        Assert.Equal(
            Enum.GetValues<BusinessType>().OrderBy(t => t),
            PlanPriceSeed.MonthlyBase.Select(b => b.Type).OrderBy(t => t));

    [Fact]
    public void PRICE_06_the_plan_screen_is_in_every_branchs_settings_whatever_its_trade()
    {
        Menu plan = MenuSeed.Build().Single(m => m.Code == "plan");

        Assert.Null(plan.BusinessType);
        Assert.Equal(AddOn.None, plan.AddOns);
        Assert.Equal(("/settings/plan", 114), (plan.RoutePath, plan.ParentId));
        Assert.Equal(
            ["settings.edit", "settings.view"],
            MenuSeed.BuildPermissions().Where(p => p.MenuId == plan.MenuId).Select(p => p.PermissionCode).Order());
    }

    // ---- The table, on a real database ------------------------------------

    /// <summary>
    /// The price table is global rather than a customer's, so a test starts it
    /// empty and seeds it itself. Tests in this collection run one at a time.
    /// </summary>
    private static async Task SeedTableAsync(AdminDbContext db)
    {
        await db.PlanPrices.ExecuteDeleteAsync();
        await PlanPriceRepository.EnsureSeededAsync(db, default);
    }

    [SkippableFact]
    public async Task PRICE_07_seeding_fills_the_table_and_a_second_start_adds_nothing()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await db.PlanPrices.ExecuteDeleteAsync();

        await PlanPriceRepository.EnsureSeededAsync(db, default);
        int first = await db.PlanPrices.CountAsync();
        await PlanPriceRepository.EnsureSeededAsync(db, default);

        Assert.Equal(PlanPriceSeed.Build().Count, first);
        Assert.Equal(first, await db.PlanPrices.CountAsync());
    }

    [SkippableFact]
    public async Task PRICE_08_a_start_keeps_a_price_an_operator_changed_and_restores_one_they_deleted()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await SeedTableAsync(db);

        await db.PlanPrices
            .Where(p => p.BusinessType == BusinessType.Retail && p.Plan == LicenseType.Pro && p.BillingCycle == BillingCycle.Quarterly)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Amount, 4321m));
        await db.PlanPrices
            .Where(p => p.BusinessType == BusinessType.Jewellery && p.Plan == LicenseType.Elite && p.BillingCycle == BillingCycle.BiAnnual)
            .ExecuteDeleteAsync();

        await PlanPriceRepository.EnsureSeededAsync(db, default);

        decimal edited = await db.PlanPrices.AsNoTracking()
            .Where(p => p.BusinessType == BusinessType.Retail && p.Plan == LicenseType.Pro && p.BillingCycle == BillingCycle.Quarterly)
            .Select(p => p.Amount).SingleAsync();
        decimal restored = await db.PlanPrices.AsNoTracking()
            .Where(p => p.BusinessType == BusinessType.Jewellery && p.Plan == LicenseType.Elite && p.BillingCycle == BillingCycle.BiAnnual)
            .Select(p => p.Amount).SingleAsync();

        Assert.Equal(4321m, edited);
        Assert.Equal(61200m, restored);
    }

    [SkippableFact]
    public async Task PRICE_09_a_branch_is_shown_the_prices_of_its_own_trade_cycle_by_cycle()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await SeedTableAsync(db);
        (_, Guid orgId, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Pro, seated: 0);
        await db.Organizations.Where(o => o.OrgId == orgId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.BusinessType, BusinessType.Jewellery));

        PlanPricesResponse prices = await new PlanPriceRepository(db).GetForBranchAsync(orgId, default);

        Assert.Equal(("Jewellery", "INR"), (prices.BusinessType, prices.CurrencyCode));

        // What each plan allows comes with the prices, from the table a licence is given its limits from.
        Assert.Equal(
            [("Professional", 5, 1000, 1000), ("Elite", null, null, null)],
            prices.Tiers.Select(t => (t.Plan, t.MaxUsers, t.MonthlySalesTransactionLimit, t.MonthlyPurchaseTransactionLimit)));
        Assert.Equal(
            [(BillingCycle.Quarterly, 3, 10800m, 32400m), (BillingCycle.BiAnnual, 6, 20400m, 61200m)],
            prices.Cycles.Select(c => (c.Cycle, c.Months, c.Professional, c.Elite)));
    }

    [SkippableFact]
    public async Task PRICE_10_the_screen_shows_the_figure_an_operator_set()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await SeedTableAsync(db);
        await db.PlanPrices
            .Where(p => p.BusinessType == BusinessType.Pharma && p.Plan == LicenseType.Elite && p.BillingCycle == BillingCycle.Quarterly)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Amount, 9999.50m));

        PlanPricesResponse prices = await new PlanPriceRepository(db).GetAsync(BusinessType.Pharma, default);

        Assert.Equal(9999.50m, prices.Cycles.Single(c => c.Cycle == BillingCycle.Quarterly).Elite);
        Assert.Equal(4050m, prices.Cycles.Single(c => c.Cycle == BillingCycle.Quarterly).Professional);
    }

    [SkippableFact]
    public async Task PRICE_11_a_branch_with_no_trade_a_trade_nobody_priced_or_no_such_branch_is_shown_no_cycles()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await SeedTableAsync(db);
        (_, Guid hrOnlyOrg, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Pro, seated: 0);
        await db.Organizations.Where(o => o.OrgId == hrOnlyOrg)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.BusinessType, (BusinessType?)null));
        var prices = new PlanPriceRepository(db);

        PlanPricesResponse hrOnly = await prices.GetForBranchAsync(hrOnlyOrg, default);
        PlanPricesResponse nobody = await prices.GetForBranchAsync(Guid.NewGuid(), default);
        Assert.Null(hrOnly.BusinessType);
        Assert.Empty(hrOnly.Cycles);
        Assert.Empty(nobody.Cycles);

        // With no price to show, the screen can still say what each plan allows.
        Assert.Equal(["Professional", "Elite"], hrOnly.Tiers.Select(t => t.Plan));

        // A trade with no rows at all: the branch is named, the cycles are empty.
        await db.PlanPrices.Where(p => p.BusinessType == BusinessType.Restaurant).ExecuteDeleteAsync();
        PlanPricesResponse unpriced = await prices.GetAsync(BusinessType.Restaurant, default);
        Assert.Equal("Restaurant", unpriced.BusinessType);
        Assert.Empty(unpriced.Cycles);
    }

    [SkippableFact]
    public async Task PRICE_12_a_cycle_is_shown_only_when_both_plans_have_a_live_price_in_it()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await SeedTableAsync(db);
        await db.PlanPrices
            .Where(p => p.BusinessType == BusinessType.School && p.Plan == LicenseType.Elite && p.BillingCycle == BillingCycle.BiAnnual)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));

        PlanPricesResponse school = await new PlanPriceRepository(db).GetAsync(BusinessType.School, default);

        Assert.Equal([BillingCycle.Quarterly], school.Cycles.Select(c => c.Cycle));
    }

    [SkippableFact]
    public async Task PRICE_13_one_price_is_kept_for_a_trade_plan_and_cycle()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await SeedTableAsync(db);

        db.PlanPrices.Add(new PlanPrice
        {
            BusinessType = BusinessType.Retail,
            Plan = LicenseType.Pro,
            BillingCycle = BillingCycle.Quarterly,
            Amount = 1m,
            CurrencyCode = "INR",
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

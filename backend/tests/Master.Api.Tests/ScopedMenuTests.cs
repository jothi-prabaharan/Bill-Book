using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The menu follows the branch's business type and add-ons (D-36, TK-136), not
/// an app the caller signed into: a shared screen in every branch, a trading
/// screen only in a branch with a trade.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class ScopedMenuTests
{
    private readonly AdminFixture _admin;

    public ScopedMenuTests(AdminFixture admin) => _admin = admin;

    private static IEnumerable<string> ScreenCodes(IReadOnlyList<MenuView> menu) =>
        menu.SelectMany(m => m.Groups).SelectMany(g => g.SubMenus).Select(s => s.Code);

    private async Task<IReadOnlyList<MenuView>> MenuFor(BusinessType? businessType, AddOn addOns)
    {
        await using AdminDbContext db = _admin.CreateContext();
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        string suffix = customerId.ToString("N")[..8];

        db.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            CountryPrefix = "IN",
            Name = $"Menu Scope {suffix}",
            BillingEmail = $"billing-{suffix}@example.com",
            Status = TenantStatus.Active,
            PlanTier = PlanTier.Standard,
            DatabaseName = "IN000001",
        });
        db.Organizations.Add(new Organization
        {
            OrgId = orgId,
            CustomerId = customerId,
            OrgCode = $"M{suffix}",
            Name = "Chennai",
            BaseCurrency = "INR",
            FinancialYearStartMonth = 4,
            CountryId = 1,
            Status = TenantStatus.Active,
            BusinessType = businessType,
            AddOns = addOns,
        });
        await db.SaveChangesAsync();

        var tenant = new TenantContext
        {
            CustomerId = customerId,
            OrgId = orgId,
            Permissions = new HashSet<string> { "settings.view", "sales.view", "accounting.view" },
        };
        return await new MenuRepository(db, tenant).GetUserMenuAsync(default);
    }

    [SkippableFact]
    public async Task A_payroll_only_branch_has_the_shared_settings_screens_and_no_trading_screen()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        List<string> payroll = [.. ScreenCodes(await MenuFor(null, AddOn.Payroll))];

        Assert.Contains("usr", payroll);
        Assert.Contains("rol", payroll);
        Assert.DoesNotContain("inv", payroll);
        Assert.DoesNotContain("tax", payroll);
    }

    [SkippableFact]
    public async Task A_retail_branch_has_its_trading_screens_and_the_shared_ones()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        List<string> retail = [.. ScreenCodes(await MenuFor(BusinessType.Retail, AddOn.None))];

        Assert.Contains("inv", retail);
        Assert.Contains("usr", retail);
    }

    [SkippableFact]
    public async Task Settings_offers_no_applications_screen_any_more()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        List<string> retail = [.. ScreenCodes(await MenuFor(BusinessType.Retail, AddOn.All))];

        Assert.DoesNotContain("lic", retail);
    }
}

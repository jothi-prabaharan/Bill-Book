using Master.Api.Services;
using Master.Entity.Models;
using Master.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>The menu follows the app (H0.3, TK-44): a shared screen in every app, a RetailErp screen only in RetailErp.</summary>
[Collection(nameof(AdminCollection))]
public sealed class AppMenuTests
{
    private readonly AdminFixture _admin;

    public AppMenuTests(AdminFixture admin) => _admin = admin;

    private static IEnumerable<string> ScreenCodes(IReadOnlyList<MenuView> menu) =>
        menu.SelectMany(m => m.Groups).SelectMany(g => g.SubMenus).Select(s => s.Code);

    private async Task<IReadOnlyList<MenuView>> MenuFor(App app)
    {
        await using AdminDbContext db = _admin.CreateContext();
        var tenant = new TenantContext
        {
            Permissions = new HashSet<string> { "settings.view", "sales.view", "accounting.view" },
        };
        return await new MenuService(db, tenant).GetUserMenuAsync(default, app);
    }

    [SkippableFact]
    public async Task A_payroll_menu_has_the_shared_settings_screens_and_no_retail_screen()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        List<string> payroll = [.. ScreenCodes(await MenuFor(App.Payroll))];

        Assert.Contains("usr", payroll);
        Assert.Contains("rol", payroll);
        Assert.Contains("lic", payroll);
        Assert.DoesNotContain("inv", payroll);
        Assert.DoesNotContain("tax", payroll);
    }

    [SkippableFact]
    public async Task A_retail_menu_is_unchanged()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        List<string> retail = [.. ScreenCodes(await MenuFor(App.RetailErp))];

        Assert.Contains("inv", retail);
        Assert.Contains("usr", retail);
    }
}

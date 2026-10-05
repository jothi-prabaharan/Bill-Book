using Master.Entity.TableEntities;
using Master.Repository.Repositories;
using Master.Repository.SeedData;
using Shared.Kernel.Apps;
using Shared.Kernel.Restaurant;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The Restaurant rail (TK-143). Pure — the menu is <c>HasData</c>, so asserting
/// the seed is asserting every database.
/// </summary>
public sealed class RestaurantMenuTests
{
    private static readonly string[] RailCodes = ["rst-orders", "rst-tables", "rst-kitchen", "pos-menu", "rst-recipes", "rst-wastage", "rst-reports"];

    [Fact]
    public void Every_restaurant_row_is_routed_and_restaurant_only()
    {
        IReadOnlyList<Menu> menu = MenuSeed.Build();

        foreach (string code in RailCodes)
        {
            Menu row = Assert.Single(menu, m => m.Code == code);
            Assert.True(row.IsActive, $"{code} is inactive.");
            Assert.StartsWith("/restaurant/", row.RoutePath, StringComparison.Ordinal);
            Assert.Equal("restaurant", row.Module);
            Assert.Equal(BusinessType.Restaurant, row.BusinessType);
        }

        Menu settings = Assert.Single(menu, m => m.Code == "rst");
        Assert.Equal("/restaurant/settings", settings.RoutePath);
        Assert.Equal(BusinessType.Restaurant, settings.BusinessType);
    }

    [Fact]
    public void Every_restaurant_row_has_a_view_permission()
    {
        IReadOnlyList<Menu> menu = MenuSeed.Build();
        IReadOnlyList<MenuPermission> permissions = MenuSeed.BuildPermissions();

        foreach (string code in RailCodes)
        {
            int id = menu.Single(m => m.Code == code).MenuId;
            Assert.Contains(permissions, p => p.MenuId == id && p.PermissionCode == "restaurant.view");
        }
    }

    [Fact]
    public void A_switch_that_is_off_hides_its_screens()
    {
        IReadOnlySet<string> hidden = MenuRepository.HiddenRestaurantCodes(new RestaurantSettingsView());

        Assert.Equal(["rst-kitchen", "rst-recipes", "rst-tables", "rst-wastage"], hidden.Order());
    }

    [Fact]
    public void Every_switch_on_hides_nothing()
    {
        RestaurantSettingsView all = new() { TablesEnabled = true, KitchenEnabled = true, RecipesEnabled = true };

        Assert.Empty(MenuRepository.HiddenRestaurantCodes(all));
    }
}

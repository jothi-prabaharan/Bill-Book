using Master.Entity.TableEntities;
using Master.Repository.Repositories;
using Master.Repository.SeedData;
using Shared.Kernel.Apps;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The till's menu row (TK-155): Sales › Point of sale at <c>/pos</c>, shown
/// only to a branch with the POS add-on and only to someone who may post.
/// </summary>
public sealed class PosMenuTests
{
    private static Menu Till() => MenuSeed.Build().Single(m => m.Code == "pos");

    [Fact]
    public void The_till_is_a_sales_screen_at_pos_for_the_pos_add_on()
    {
        Menu till = Till();

        Assert.Equal("/pos", till.RoutePath);
        Assert.Equal("sales", till.Module);
        Assert.Null(till.BusinessType);
        Assert.Equal(AddOn.Pos, till.AddOns);
        Assert.Contains(MenuSeed.BuildPermissions(), p => p.MenuId == till.MenuId && p.PermissionCode == "sales.approve");
    }

    [Theory]
    [InlineData(BusinessType.Retail, AddOn.Pos, true)]
    [InlineData(BusinessType.Pharma, AddOn.Pos | AddOn.Crm, true)]
    [InlineData(BusinessType.Retail, AddOn.None, false)]
    [InlineData(BusinessType.School, AddOn.None, false)]
    [InlineData(null, AddOn.Hrms, false)]
    public void Only_a_branch_with_the_pos_add_on_sees_it(BusinessType? branch, AddOn addOns, bool shown)
    {
        Menu till = Till();
        Assert.Equal(shown, MenuRepository.MenuMatches(branch, addOns, till.BusinessType, till.AddOns));
    }
}

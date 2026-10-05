using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Repository.SeedData;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Shared.Kernel.Apps;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The menu follows the branch's trade (D-10, TK-30), and the branch's trade
/// travels as the enum while its wire format stays the name.
/// </summary>
public sealed class TradeScopeTests
{
    [Theory]
    [InlineData(Vertical.Retail, true)]
    [InlineData(Vertical.Jewellery, true)]
    [InlineData(Vertical.Pharma, false)]
    [InlineData(Vertical.Service, false)]
    [InlineData(Vertical.Restaurant, false)]
    public void Metal_purity_is_shown_to_retail_and_jewellery_branches_only(Vertical trade, bool shown)
    {
        Assert.Equal(shown, TradeScopeRepository.ShowsMenu("mtp", trade));
    }

    [Theory]
    [InlineData(Vertical.Retail)]
    [InlineData(Vertical.Pharma)]
    [InlineData(Vertical.Jewellery)]
    public void A_screen_no_trade_claims_is_every_trades(Vertical trade)
    {
        Assert.True(TradeScopeRepository.ShowsMenu("usr", trade));
        Assert.True(TradeScopeRepository.ShowsMenu("smtp", trade));
    }

    [Fact]
    public void A_service_branch_sees_no_stock_screen_but_keeps_its_items()
    {
        // TK-136: a service business holds no stock.
        Assert.All(TradeScopeRepository.StockOnlyMenus, code =>
            Assert.False(TradeScopeRepository.ShowsMenu(code, Vertical.Service), code));

        foreach (string kept in new[] { "itm", "cat", "uom", "hsn", "inv", "bil", "por", "qot" })
        {
            Assert.True(TradeScopeRepository.ShowsMenu(kept, Vertical.Service), kept);
        }
    }

    [Theory]
    [InlineData(Vertical.Retail)]
    [InlineData(Vertical.Pharma)]
    [InlineData(Vertical.Jewellery)]
    [InlineData(Vertical.Restaurant)]
    public void Every_trade_that_holds_stock_sees_the_stock_screens(Vertical trade) =>
        Assert.All(TradeScopeRepository.StockOnlyMenus, code =>
            Assert.True(TradeScopeRepository.ShowsMenu(code, trade), code));

    [Theory]
    [InlineData(BusinessType.Retail, true)]
    [InlineData(BusinessType.Pharma, true)]
    [InlineData(BusinessType.Jewellery, true)]
    [InlineData(BusinessType.Restaurant, true)]
    [InlineData(BusinessType.Service, true)]
    [InlineData(BusinessType.School, false)]
    public void The_trading_menus_reach_every_trading_business(BusinessType branch, bool shown) =>
        Assert.Equal(shown, MenuRepository.MenuMatches(branch, AddOn.None, BusinessType.Retail, AddOn.None));

    [Fact]
    public void Every_trade_scoped_code_is_a_real_menu_row()
    {
        HashSet<string> codes = MenuSeed.Build().Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A code listed here but absent from the seed would scope nothing and
        // read as if it did.
        Assert.All(TradeScopeRepository.MenuTrades.Keys, code => Assert.Contains(code, codes));
    }

    [Fact]
    public void The_branch_request_reads_and_writes_the_trade_by_name()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        SaveOrganizationRequest request = JsonSerializer.Deserialize<SaveOrganizationRequest>(
            """{"vertical":"Jewellery"}""", web)!;
        Assert.Equal(Vertical.Jewellery, request.Vertical);

        string written = JsonSerializer.Serialize(new OrganizationListItem { Vertical = Vertical.Pharma }, web);
        Assert.Contains("\"vertical\":\"Pharma\"", written);
    }

    [Fact]
    public void An_undefined_trade_is_refused_by_validation()
    {
        var request = new SaveOrganizationRequest { Vertical = (Vertical)99 };
        var results = new List<ValidationResult>();

        Validator.TryValidateProperty(request.Vertical, new ValidationContext(request) { MemberName = nameof(request.Vertical) }, results);

        Assert.Contains(results, r => r.ErrorMessage == "Vertical must be Retail, Pharma, Jewellery, School, Service or Restaurant.");
    }
}

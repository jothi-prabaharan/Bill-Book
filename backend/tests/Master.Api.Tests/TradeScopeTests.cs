using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Repository.SeedData;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The menu follows the branch's trade (D-10, TK-30), and the branch's trade
/// travels as the enum while its wire format stays the name.
/// </summary>
public sealed class TradeScopeTests
{
    [Theory]
    [InlineData(Vertical.General, true)]
    [InlineData(Vertical.Jewellery, true)]
    [InlineData(Vertical.Pharma, false)]
    public void Metal_purity_is_shown_to_general_and_jewellery_branches_only(Vertical trade, bool shown)
    {
        Assert.Equal(shown, TradeScope.ShowsMenu("mtp", trade));
    }

    [Theory]
    [InlineData(Vertical.General)]
    [InlineData(Vertical.Pharma)]
    [InlineData(Vertical.Jewellery)]
    public void A_screen_no_trade_claims_is_every_trades(Vertical trade)
    {
        Assert.True(TradeScope.ShowsMenu("usr", trade));
        Assert.True(TradeScope.ShowsMenu("smtp", trade));
    }

    [Fact]
    public void Every_trade_scoped_code_is_a_real_menu_row()
    {
        HashSet<string> codes = MenuSeed.Build().Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A code listed here but absent from the seed would scope nothing and
        // read as if it did.
        Assert.All(TradeScope.MenuTrades.Keys, code => Assert.Contains(code, codes));
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

        Assert.Contains(results, r => r.ErrorMessage == "Vertical must be General, Pharma or Jewellery.");
    }
}

using Shared.Kernel.Restaurant;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>What a bill earns in loyalty points, and the branch's rate (TK-166).</summary>
public sealed class LoyaltyRulesTests
{
    [Theory]
    [InlineData(250, 1, 2)]      // ₹250 at a point per ₹100: two whole points
    [InlineData(99.99, 1, 0)]    // not yet a whole ₹100
    [InlineData(1000, 2.5, 25)]
    [InlineData(1000, 0, 0)]     // no rate, no points
    [InlineData(0, 5, 0)]
    [InlineData(-200, 5, 0)]
    public void A_bill_earns_whole_points_at_the_rate_per_hundred(double total, double rate, int points) =>
        Assert.Equal(points, LoyaltyRules.PointsFor((decimal)total, (decimal)rate));

    [Fact]
    public void The_rate_defaults_to_nothing_earned()
    {
        RestaurantSettingsView view = RestaurantSettingsRules.Effective(new Dictionary<RestaurantSettingKey, string?>());
        Assert.Equal(0m, view.LoyaltyEarnRate);
        Assert.Equal("restaurant.loyalty.earnRate", RestaurantSettingKey.LoyaltyEarnRate.Code());
    }

    [Fact]
    public void A_stored_rate_reaches_the_view_and_a_bad_one_falls_back_to_none()
    {
        Assert.Equal(1.5m, RestaurantSettingsRules.Effective(new Dictionary<RestaurantSettingKey, string?> { [RestaurantSettingKey.LoyaltyEarnRate] = "1.5" }).LoyaltyEarnRate);
        Assert.Equal(0m, RestaurantSettingsRules.Effective(new Dictionary<RestaurantSettingKey, string?> { [RestaurantSettingKey.LoyaltyEarnRate] = "500" }).LoyaltyEarnRate);
    }

    [Theory]
    [InlineData("1", "1")]
    [InlineData(" 2.555 ", "2.56")]
    [InlineData("100", "100")]
    public void A_rate_between_0_and_100_is_stored(string value, string stored) =>
        Assert.Equal(stored, RestaurantSettingKeys.Normalise(RestaurantSettingKey.LoyaltyEarnRate, value).Stored);

    [Theory]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("lots")]
    public void A_rate_out_of_range_is_refused(string value) =>
        Assert.Contains("Loyalty points per ₹100", RestaurantSettingKeys.Normalise(RestaurantSettingKey.LoyaltyEarnRate, value).Refusal);
}

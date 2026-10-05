using Shared.Kernel.Internal;
using Shared.Kernel.Restaurant;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>Restaurant settings (D-39, TK-143): stored values, the effective view and the feature guard.</summary>
public sealed class RestaurantSettingsTests
{
    private static RestaurantSettingsView Effective(params (RestaurantSettingKey Key, string? Value)[] stored) =>
        RestaurantSettingsRules.Effective(stored.ToDictionary(s => s.Key, s => s.Value));

    [Fact]
    public void A_new_branch_is_billing_only_with_nothing_required()
    {
        RestaurantSettingsView view = Effective();

        Assert.False(view.TablesEnabled);
        Assert.False(view.KitchenEnabled);
        Assert.False(view.RecipesEnabled);
        Assert.Equal(RequirementLevel.Off, view.TableRequired);
        Assert.Equal(RequirementLevel.Off, view.CustomerRequired);
        Assert.False(view.KotRequired);
        Assert.Equal(0m, view.ServiceChargePercent);
    }

    [Fact]
    public void A_table_cannot_be_required_while_tables_are_off()
    {
        RestaurantSettingsView view = Effective(
            (RestaurantSettingKey.TablesEnabled, "false"),
            (RestaurantSettingKey.TableRequired, "Always"));

        Assert.Equal(RequirementLevel.Off, view.TableRequired);
        Assert.False(view.IsTableRequired(OrderType.DineIn));
    }

    [Fact]
    public void A_kitchen_ticket_cannot_be_required_while_the_kitchen_is_off()
    {
        Assert.False(Effective((RestaurantSettingKey.KotRequired, "true")).KotRequired);
        Assert.True(Effective((RestaurantSettingKey.KitchenEnabled, "true"), (RestaurantSettingKey.KotRequired, "true")).KotRequired);
    }

    [Fact]
    public void Dine_in_only_applies_to_dine_in_orders_alone()
    {
        RestaurantSettingsView view = Effective(
            (RestaurantSettingKey.TablesEnabled, "true"),
            (RestaurantSettingKey.TableRequired, "DineInOnly"),
            (RestaurantSettingKey.CustomerRequired, "Always"));

        Assert.True(view.IsTableRequired(OrderType.DineIn));
        Assert.False(view.IsTableRequired(OrderType.Takeaway));
        Assert.True(view.IsCustomerRequired(OrderType.Delivery));
    }

    [Fact]
    public void A_stored_value_that_does_not_parse_takes_the_default()
    {
        RestaurantSettingsView view = Effective(
            (RestaurantSettingKey.TablesEnabled, "yes"),
            (RestaurantSettingKey.ServiceChargePercent, "90"));

        Assert.False(view.TablesEnabled);
        Assert.Equal(0m, view.ServiceChargePercent);
    }

    [Theory]
    [InlineData(RestaurantSettingKey.TablesEnabled, " True ", "true")]
    [InlineData(RestaurantSettingKey.TableRequired, "dineinonly", "DineInOnly")]
    [InlineData(RestaurantSettingKey.ServiceChargePercent, "12.345", "12.35")]
    [InlineData(RestaurantSettingKey.ServiceChargePercent, "25", "25")]
    public void Normalise_stores_one_spelling(RestaurantSettingKey key, string value, string stored)
    {
        (string? actual, string? refusal) = RestaurantSettingKeys.Normalise(key, value);

        Assert.Null(refusal);
        Assert.Equal(stored, actual);
    }

    [Theory]
    [InlineData(RestaurantSettingKey.KotRequired, "1")]
    [InlineData(RestaurantSettingKey.CustomerRequired, "2")]
    [InlineData(RestaurantSettingKey.CustomerRequired, "Sometimes")]
    [InlineData(RestaurantSettingKey.ServiceChargePercent, "25.01")]
    [InlineData(RestaurantSettingKey.ServiceChargePercent, "-1")]
    [InlineData(RestaurantSettingKey.ServiceChargePercent, null)]
    public void Normalise_refuses_what_it_cannot_store(RestaurantSettingKey key, string? value)
    {
        (string? stored, string? refusal) = RestaurantSettingKeys.Normalise(key, value);

        Assert.Null(stored);
        Assert.NotNull(refusal);
    }

    [Fact]
    public void Every_key_has_a_code_that_round_trips()
    {
        foreach (RestaurantSettingKey key in RestaurantSettingKeys.All)
        {
            Assert.True(RestaurantSettingKeys.TryParse(key.Code(), out RestaurantSettingKey parsed));
            Assert.Equal(key, parsed);
            Assert.Null(RestaurantSettingKeys.Normalise(key, key.DefaultValue()).Refusal);
        }
    }

    [Fact]
    public void The_feature_guard_names_the_switch_that_is_off()
    {
        RestaurantSettingsView view = Effective((RestaurantSettingKey.TablesEnabled, "true"));

        Assert.Null(RequireRestaurantFeatureAttribute.Refusal(RestaurantFeature.Tables, view));
        Assert.Contains("kitchen", RequireRestaurantFeatureAttribute.Refusal(RestaurantFeature.Kitchen, view), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Recipes", RequireRestaurantFeatureAttribute.Refusal(RestaurantFeature.Tables | RestaurantFeature.Recipes, view));
    }
}

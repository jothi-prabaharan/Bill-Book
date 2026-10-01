using System.Text.Json.Serialization;
namespace Shared.Kernel.Restaurant;

public enum RequirementLevel
{
    Off = 0,
    DineInOnly = 1,
    Always = 2
}

public static class RestaurantSettingKey
{
    public const string TablesEnabled = "restaurant.tables.enabled";
    public const string KitchenEnabled = "restaurant.kitchen.enabled";
    public const string RecipesEnabled = "restaurant.recipes.enabled";
    public const string TableRequired = "restaurant.table.required";
    public const string KotRequired = "restaurant.kot.required";
    public const string CustomerRequired = "restaurant.customer.required";
    public const string WaiterRequired = "restaurant.waiter.required";
    public const string ServiceChargePercent = "restaurant.serviceCharge.percent";
    public const string TipsEnabled = "restaurant.tips.enabled";
}

public class RestaurantSettingsView
{
    public bool TablesEnabled { get; set; }
    public bool KitchenEnabled { get; set; }
    public bool RecipesEnabled { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RequirementLevel TableRequired { get; set; }
    public bool KotRequired { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RequirementLevel CustomerRequired { get; set; }
    public bool WaiterRequired { get; set; }
    public decimal ServiceChargePercent { get; set; }
    public bool TipsEnabled { get; set; }
}


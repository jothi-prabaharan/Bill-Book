using System.Globalization;
using System.Text.Json.Serialization;

namespace Shared.Kernel.Restaurant;

/// <summary>When a field is demanded of a restaurant bill (D-39, TK-143).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RequirementLevel
{
    Off = 0,
    DineInOnly = 1,
    Always = 2,
}

/// <summary>
/// A Restaurant branch's settings (D-39, TK-143), stored as typed keys in
/// <c>mst.Configuration</c>: a system default row and a per-branch override,
/// no table of their own. Rule 7: one enum, each member with a fixed string
/// code, rather than strings scattered through the services.
/// </summary>
public enum RestaurantSettingKey
{
    TablesEnabled = 1,
    KitchenEnabled = 2,
    RecipesEnabled = 3,
    TableRequired = 4,
    KotRequired = 5,
    CustomerRequired = 6,
    WaiterRequired = 7,
    ServiceChargePercent = 8,
    TipsEnabled = 9,
}

/// <summary>What kind of value a key holds.</summary>
public enum RestaurantSettingType
{
    Boolean = 1,
    Level = 2,
    Percent = 3,
}

/// <summary>The fixed code, type, default and validation of each <see cref="RestaurantSettingKey"/>.</summary>
public static class RestaurantSettingKeys
{
    /// <summary>A service charge above this is refused: no Indian restaurant levies more, and a typo should not reach a bill.</summary>
    public const decimal MaxServiceChargePercent = 25m;

    public static IReadOnlyList<RestaurantSettingKey> All { get; } = Enum.GetValues<RestaurantSettingKey>();

    /// <summary>The <c>mst.Configuration</c> code. Stored data: never change one.</summary>
    public static string Code(this RestaurantSettingKey key) => key switch
    {
        RestaurantSettingKey.TablesEnabled => "restaurant.tables.enabled",
        RestaurantSettingKey.KitchenEnabled => "restaurant.kitchen.enabled",
        RestaurantSettingKey.RecipesEnabled => "restaurant.recipes.enabled",
        RestaurantSettingKey.TableRequired => "restaurant.table.required",
        RestaurantSettingKey.KotRequired => "restaurant.kot.required",
        RestaurantSettingKey.CustomerRequired => "restaurant.customer.required",
        RestaurantSettingKey.WaiterRequired => "restaurant.waiter.required",
        RestaurantSettingKey.ServiceChargePercent => "restaurant.serviceCharge.percent",
        RestaurantSettingKey.TipsEnabled => "restaurant.tips.enabled",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown restaurant setting."),
    };

    public static RestaurantSettingType TypeOf(this RestaurantSettingKey key) => key switch
    {
        RestaurantSettingKey.TableRequired or RestaurantSettingKey.CustomerRequired => RestaurantSettingType.Level,
        RestaurantSettingKey.ServiceChargePercent => RestaurantSettingType.Percent,
        _ => RestaurantSettingType.Boolean,
    };

    /// <summary>Everything off and nothing required on a new Restaurant branch (D-39).</summary>
    public static string DefaultValue(this RestaurantSettingKey key) => key.TypeOf() switch
    {
        RestaurantSettingType.Level => nameof(RequirementLevel.Off),
        RestaurantSettingType.Percent => "0",
        _ => "false",
    };

    public static bool TryParse(string? code, out RestaurantSettingKey key)
    {
        foreach (RestaurantSettingKey candidate in All)
        {
            if (string.Equals(candidate.Code(), code, StringComparison.Ordinal))
            {
                key = candidate;
                return true;
            }
        }

        key = default;
        return false;
    }

    /// <summary>The value in the form it is stored, or a refusal naming what is wrong.</summary>
    public static (string? Stored, string? Refusal) Normalise(RestaurantSettingKey key, string? value)
    {
        string text = value?.Trim() ?? string.Empty;
        switch (key.TypeOf())
        {
            case RestaurantSettingType.Boolean:
                return bool.TryParse(text, out bool flag)
                    ? (flag ? "true" : "false", null)
                    : (null, "A switch is on or off.");

            case RestaurantSettingType.Level:
                return Enum.TryParse(text, ignoreCase: true, out RequirementLevel level) && Enum.IsDefined(level) && !int.TryParse(text, out _)
                    ? (level.ToString(), null)
                    : (null, "Choose Off, Dine-in only or Always.");

            default:
                return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal percent)
                    && percent >= 0m && percent <= MaxServiceChargePercent
                    ? (Math.Round(percent, 2).ToString(CultureInfo.InvariantCulture), null)
                    : (null, $"The service charge must be between 0 and {MaxServiceChargePercent}%.");
        }
    }
}

/// <summary>
/// A branch's restaurant settings as the services act on them (TK-143). Built
/// only by <see cref="RestaurantSettingsRules.Effective"/>, so a required key
/// counts only while its feature switch is on: <c>TableRequired = Always</c>
/// with tables off demands nothing. The API, the page and every service read
/// this and nothing else.
/// </summary>
public sealed class RestaurantSettingsView
{
    public bool TablesEnabled { get; set; }

    public bool KitchenEnabled { get; set; }

    public bool RecipesEnabled { get; set; }

    public RequirementLevel TableRequired { get; set; }

    public bool KotRequired { get; set; }

    public RequirementLevel CustomerRequired { get; set; }

    public bool WaiterRequired { get; set; }

    public decimal ServiceChargePercent { get; set; }

    public bool TipsEnabled { get; set; }

    public bool IsTableRequired(OrderType orderType) => Applies(TableRequired, orderType);

    public bool IsCustomerRequired(OrderType orderType) => Applies(CustomerRequired, orderType);

    private static bool Applies(RequirementLevel level, OrderType orderType) =>
        level == RequirementLevel.Always || (level == RequirementLevel.DineInOnly && orderType == OrderType.DineIn);
}

/// <summary>Turns stored values into the effective <see cref="RestaurantSettingsView"/>. Pure.</summary>
public static class RestaurantSettingsRules
{
    /// <param name="stored">Each key's effective stored value (branch override, else system default); missing keys take their defaults.</param>
    public static RestaurantSettingsView Effective(IReadOnlyDictionary<RestaurantSettingKey, string?> stored)
    {
        string Value(RestaurantSettingKey key) =>
            stored.TryGetValue(key, out string? v) && RestaurantSettingKeys.Normalise(key, v).Stored is string ok
                ? ok
                : key.DefaultValue();

        bool Flag(RestaurantSettingKey key) => Value(key) == "true";
        RequirementLevel Level(RestaurantSettingKey key) => Enum.Parse<RequirementLevel>(Value(key));

        bool tables = Flag(RestaurantSettingKey.TablesEnabled);
        bool kitchen = Flag(RestaurantSettingKey.KitchenEnabled);

        return new RestaurantSettingsView
        {
            TablesEnabled = tables,
            KitchenEnabled = kitchen,
            RecipesEnabled = Flag(RestaurantSettingKey.RecipesEnabled),

            // Needs its switch: a table cannot be demanded where there are none.
            TableRequired = tables ? Level(RestaurantSettingKey.TableRequired) : RequirementLevel.Off,
            KotRequired = kitchen && Flag(RestaurantSettingKey.KotRequired),

            // Billing's own fields, needing no switch.
            CustomerRequired = Level(RestaurantSettingKey.CustomerRequired),
            WaiterRequired = Flag(RestaurantSettingKey.WaiterRequired),
            ServiceChargePercent = decimal.Parse(Value(RestaurantSettingKey.ServiceChargePercent), CultureInfo.InvariantCulture),
            TipsEnabled = Flag(RestaurantSettingKey.TipsEnabled),
        };
    }
}

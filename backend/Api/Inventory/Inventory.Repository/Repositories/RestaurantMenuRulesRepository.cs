using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Shared.Kernel.Restaurant;

namespace Inventory.Repository.Repositories;

/// <summary>
/// The restaurant menu's rules (TK-144), pure so they can be tested without a
/// database: when a timed menu is on, what a line costs, which choices a line
/// may carry, and what a saved dish may look like.
/// </summary>
public static class RestaurantMenuRulesRepository
{
    public static MenuDays DayFlag(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => MenuDays.Monday,
        DayOfWeek.Tuesday => MenuDays.Tuesday,
        DayOfWeek.Wednesday => MenuDays.Wednesday,
        DayOfWeek.Thursday => MenuDays.Thursday,
        DayOfWeek.Friday => MenuDays.Friday,
        DayOfWeek.Saturday => MenuDays.Saturday,
        _ => MenuDays.Sunday,
    };

    /// <summary>
    /// Whether a dish on <paramref name="schedule"/> is served at <paramref name="at"/>
    /// (branch local time). No schedule: always. A switched-off schedule: never.
    /// The start is included and the end is not. An end before the start runs
    /// past midnight, and the hours after midnight count as the day it began,
    /// so a Friday 22:00–02:00 late menu is on at 01:00 on Saturday.
    /// </summary>
    public static bool IsServedAt(MenuSchedule? schedule, DateTime at)
    {
        if (schedule is null)
        {
            return true;
        }

        if (!schedule.IsActive)
        {
            return false;
        }

        TimeOnly time = TimeOnly.FromDateTime(at);
        bool today = schedule.Days.HasFlag(DayFlag(at.DayOfWeek));

        if (schedule.StartTime < schedule.EndTime)
        {
            return today && time >= schedule.StartTime && time < schedule.EndTime;
        }

        bool yesterday = schedule.Days.HasFlag(DayFlag(at.AddDays(-1).DayOfWeek));
        return (today && time >= schedule.StartTime) || (yesterday && time < schedule.EndTime);
    }

    /// <summary>
    /// The price of a dish or one of its portions for an order type: the
    /// order-type price when there is one, else the portion's own price, else
    /// the item's sales price. Null when nothing prices it.
    /// </summary>
    public static decimal? PriceOf(decimal? itemPrice, ItemVariant? variant, IEnumerable<MenuItemPrice> prices, OrderType orderType)
    {
        long? variantId = variant?.ItemVariantId;
        MenuItemPrice? specific = prices.FirstOrDefault(p => p.ItemVariantId == variantId && p.OrderType == orderType);
        return specific?.Price ?? variant?.Price ?? itemPrice;
    }

    /// <summary>
    /// Why the chosen modifiers cannot go on a line of this dish, or null. Every
    /// choice must belong to the dish and be available, none twice, and each
    /// group's count must be within its minimum and maximum.
    /// </summary>
    public static string? ModifierRefusal(
        IReadOnlyCollection<ModifierGroup> groups, IReadOnlyCollection<Modifier> modifiers, IReadOnlyCollection<long> chosen)
    {
        if (chosen.Distinct().Count() != chosen.Count)
        {
            return "The same choice was picked twice.";
        }

        Dictionary<long, Modifier> byId = modifiers.ToDictionary(m => m.ModifierId);
        foreach (long id in chosen)
        {
            if (!byId.TryGetValue(id, out Modifier? modifier))
            {
                return "One of the choices is not offered with this dish.";
            }

            if (!modifier.IsAvailable)
            {
                return $"{modifier.Name} is sold out.";
            }
        }

        foreach (ModifierGroup group in groups)
        {
            int count = chosen.Count(id => byId[id].ModifierGroupId == group.ModifierGroupId);
            if (count < group.MinChoices)
            {
                return group.MinChoices == 1
                    ? $"Choose a {group.Name.ToLowerInvariant()}."
                    : $"Choose at least {group.MinChoices} for {group.Name}.";
            }

            if (count > group.MaxChoices)
            {
                return group.MaxChoices == 1
                    ? $"Choose only one {group.Name.ToLowerInvariant()}."
                    : $"Choose at most {group.MaxChoices} for {group.Name}.";
            }
        }

        return null;
    }

    /// <summary>Why a timed menu cannot be saved, or null.</summary>
    public static string? ScheduleRefusal(SaveMenuScheduleRequest request)
    {
        if ((request.Days & MenuDays.Everyday) == MenuDays.None || (request.Days & ~MenuDays.Everyday) != MenuDays.None)
        {
            return "Choose at least one day.";
        }

        return request.StartTime == request.EndTime
            ? "A timed menu must start and end at different times."
            : null;
    }

    /// <summary>
    /// Why a dish cannot be saved as sent, or null: names unique within their
    /// parent, every price naming a portion in the request, one price per order
    /// type and portion, no dish-level price once portions exist, a group
    /// asking for no more than it offers, and a stock link carrying its quantity.
    /// </summary>
    public static string? MenuItemRefusal(SaveMenuItemRequest request)
    {
        if (Duplicate(request.Variants.Select(v => v.Name)) is string variant)
        {
            return $"Two portions are named {variant}.";
        }

        HashSet<string> variantNames = new(request.Variants.Select(v => v.Name.Trim()), StringComparer.OrdinalIgnoreCase);
        HashSet<(OrderType, string)> priced = [];
        foreach (SaveMenuItemPriceRequest price in request.Prices)
        {
            string? name = string.IsNullOrWhiteSpace(price.VariantName) ? null : price.VariantName.Trim();
            if (name is null && variantNames.Count > 0)
            {
                return "This dish is sold by portion, so price each portion for the order type instead.";
            }

            if (name is not null && !variantNames.Contains(name))
            {
                return $"A price names the portion {name}, which this dish does not have.";
            }

            if (!priced.Add((price.OrderType, (name ?? string.Empty).ToUpperInvariant())))
            {
                return name is null
                    ? $"The dish has two {OrderTypeLabel(price.OrderType)} prices."
                    : $"{name} has two {OrderTypeLabel(price.OrderType)} prices.";
            }
        }

        if (Duplicate(request.ModifierGroups.Select(g => g.Name)) is string group)
        {
            return $"Two choice groups are named {group}.";
        }

        foreach (SaveModifierGroupRequest g in request.ModifierGroups)
        {
            if (g.MinChoices > g.MaxChoices)
            {
                return $"{g.Name}: the minimum cannot be more than the maximum.";
            }

            if (g.Modifiers.Count == 0)
            {
                return $"{g.Name} has no choices in it.";
            }

            if (g.MinChoices > g.Modifiers.Count)
            {
                return $"{g.Name} asks for {g.MinChoices} choices but offers {g.Modifiers.Count}.";
            }

            if (Duplicate(g.Modifiers.Select(m => m.Name)) is string modifier)
            {
                return $"{g.Name} has two choices named {modifier}.";
            }

            if (g.Modifiers.Any(m => (m.LinkedItemId is null) != (m.LinkedQuantity is null)))
            {
                return $"{g.Name}: a choice that uses up an item needs both the item and the quantity.";
            }
        }

        return null;
    }

    public static string OrderTypeLabel(OrderType orderType) => orderType switch
    {
        OrderType.DineIn => "dine-in",
        OrderType.Takeaway => "takeaway",
        OrderType.Delivery => "delivery",
        _ => "room service",
    };

    private static string? Duplicate(IEnumerable<string> names) =>
        names.Select(n => n.Trim())
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1)?.Key;
}

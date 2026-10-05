using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository.Repositories;
using Shared.Kernel.Restaurant;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>The restaurant menu's pure rules (TK-144): timed menus, prices and choices.</summary>
public sealed class RestaurantMenuRulesTests
{
    // 5 October 2026 is a Monday.
    private static DateTime Monday(int hour, int minute = 0) => new(2026, 10, 5, hour, minute, 0);

    private static MenuSchedule Schedule(MenuDays days, int from, int until, bool active = true) => new()
    {
        Name = "Timed",
        Days = days,
        StartTime = new TimeOnly(from, 0),
        EndTime = new TimeOnly(until, 0),
        IsActive = active,
    };

    [Fact]
    public void A_dish_on_no_timed_menu_is_always_served()
    {
        Assert.True(RestaurantMenuRulesRepository.IsServedAt(null, Monday(3)));
    }

    [Fact]
    public void Breakfast_is_served_from_its_start_until_just_before_its_end()
    {
        MenuSchedule breakfast = Schedule(MenuDays.Weekdays, 7, 11);

        Assert.False(RestaurantMenuRulesRepository.IsServedAt(breakfast, Monday(6, 59)));
        Assert.True(RestaurantMenuRulesRepository.IsServedAt(breakfast, Monday(7)));
        Assert.False(RestaurantMenuRulesRepository.IsServedAt(breakfast, Monday(11)));
        Assert.False(RestaurantMenuRulesRepository.IsServedAt(breakfast, Monday(8).AddDays(-1)), "Sunday is not a weekday.");
    }

    [Fact]
    public void A_late_menu_after_midnight_belongs_to_the_day_it_started()
    {
        MenuSchedule friday = Schedule(MenuDays.Friday, 22, 2);
        DateTime saturdayOne = new(2026, 10, 10, 1, 0, 0);
        DateTime fridayOne = new(2026, 10, 9, 1, 0, 0);

        Assert.True(RestaurantMenuRulesRepository.IsServedAt(friday, new DateTime(2026, 10, 9, 23, 0, 0)));
        Assert.True(RestaurantMenuRulesRepository.IsServedAt(friday, saturdayOne));
        Assert.False(RestaurantMenuRulesRepository.IsServedAt(friday, fridayOne), "01:00 on Friday is Thursday night.");
    }

    [Fact]
    public void A_switched_off_timed_menu_serves_nothing()
    {
        Assert.False(RestaurantMenuRulesRepository.IsServedAt(Schedule(MenuDays.Everyday, 0, 23, active: false), Monday(12)));
    }

    [Fact]
    public void A_half_portion_lists_at_its_own_price()
    {
        ItemVariant half = new() { ItemVariantId = 1, Name = "Half", Price = 110m, PortionFactor = 0.5m };
        ItemVariant full = new() { ItemVariantId = 2, Name = "Full", Price = 200m };

        Assert.Equal(110m, RestaurantMenuRulesRepository.PriceOf(200m, half, [], OrderType.DineIn));
        Assert.Equal(200m, RestaurantMenuRulesRepository.PriceOf(200m, full, [], OrderType.DineIn));
    }

    [Fact]
    public void An_order_type_price_wins_for_its_portion_only()
    {
        ItemVariant half = new() { ItemVariantId = 1, Name = "Half", Price = 110m };
        MenuItemPrice[] prices =
        [
            new() { ItemVariantId = 1, OrderType = OrderType.Delivery, Price = 125m },
            new() { ItemVariantId = null, OrderType = OrderType.Takeaway, Price = 210m },
        ];

        Assert.Equal(125m, RestaurantMenuRulesRepository.PriceOf(200m, half, prices, OrderType.Delivery));
        Assert.Equal(110m, RestaurantMenuRulesRepository.PriceOf(200m, half, prices, OrderType.Takeaway));
        Assert.Equal(210m, RestaurantMenuRulesRepository.PriceOf(200m, null, prices, OrderType.Takeaway));
        Assert.Equal(200m, RestaurantMenuRulesRepository.PriceOf(200m, null, prices, OrderType.DineIn));
        Assert.Null(RestaurantMenuRulesRepository.PriceOf(null, null, [], OrderType.DineIn));
    }

    private static readonly ModifierGroup Spice = new() { ModifierGroupId = 1, Name = "Spice level", MinChoices = 1, MaxChoices = 1 };
    private static readonly ModifierGroup Extras = new() { ModifierGroupId = 2, Name = "Extras", MinChoices = 0, MaxChoices = 2 };

    private static readonly Modifier[] Modifiers =
    [
        new() { ModifierId = 10, ModifierGroupId = 1, Name = "Mild", IsAvailable = true },
        new() { ModifierId = 11, ModifierGroupId = 1, Name = "Hot", IsAvailable = true },
        new() { ModifierId = 20, ModifierGroupId = 2, Name = "Cheese", PriceChange = 30m, IsAvailable = true },
        new() { ModifierId = 21, ModifierGroupId = 2, Name = "Paneer", PriceChange = 40m, IsAvailable = false },
        new() { ModifierId = 22, ModifierGroupId = 2, Name = "Olives", PriceChange = 20m, IsAvailable = true },
    ];

    [Theory]
    [InlineData(new long[] { 10 }, null)]
    [InlineData(new long[] { 11, 20, 22 }, null)]
    [InlineData(new long[] { }, "Choose a spice level.")]
    [InlineData(new long[] { 10, 11 }, "Choose only one spice level.")]
    [InlineData(new long[] { 10, 21 }, "Paneer is sold out.")]
    [InlineData(new long[] { 10, 99 }, "One of the choices is not offered with this dish.")]
    [InlineData(new long[] { 10, 10 }, "The same choice was picked twice.")]
    public void Choices_are_checked_against_their_groups(long[] chosen, string? refusal)
    {
        Assert.Equal(refusal, RestaurantMenuRulesRepository.ModifierRefusal([Spice, Extras], Modifiers, chosen));
    }

    [Fact]
    public void Too_many_extras_are_refused()
    {
        Modifier[] three = [.. Modifiers.Select(m => m.ModifierId == 21 ? new Modifier { ModifierId = 21, ModifierGroupId = 2, Name = "Paneer", IsAvailable = true } : m)];

        Assert.Equal("Choose at most 2 for Extras.", RestaurantMenuRulesRepository.ModifierRefusal([Spice, Extras], three, [10, 20, 21, 22]));
    }

    [Fact]
    public void A_timed_menu_needs_a_day_and_two_different_times()
    {
        SaveMenuScheduleRequest request = new() { Name = "Lunch", Days = MenuDays.Weekdays, StartTime = new(12, 0), EndTime = new(15, 0) };

        Assert.Null(RestaurantMenuRulesRepository.ScheduleRefusal(request));
        Assert.NotNull(RestaurantMenuRulesRepository.ScheduleRefusal(new SaveMenuScheduleRequest { Name = "x", Days = MenuDays.None, StartTime = new(1, 0), EndTime = new(2, 0) }));
        Assert.NotNull(RestaurantMenuRulesRepository.ScheduleRefusal(new SaveMenuScheduleRequest { Name = "x", Days = (MenuDays)128, StartTime = new(1, 0), EndTime = new(2, 0) }));
        Assert.NotNull(RestaurantMenuRulesRepository.ScheduleRefusal(new SaveMenuScheduleRequest { Name = "x", Days = MenuDays.Monday, StartTime = new(1, 0), EndTime = new(1, 0) }));
    }

    private static SaveMenuItemRequest Dish(params SaveItemVariantRequest[] variants) => new()
    {
        ItemId = 1,
        MenuCategoryId = 1,
        Variants = [.. variants],
    };

    [Fact]
    public void A_dish_sold_by_portion_cannot_also_have_a_dish_level_price()
    {
        SaveMenuItemRequest request = Dish(new SaveItemVariantRequest { Name = "Half", Price = 100m }, new() { Name = "Full", Price = 180m });
        request.Prices.Add(new SaveMenuItemPriceRequest { OrderType = OrderType.Delivery, Price = 200m });

        Assert.Contains("price each portion", RestaurantMenuRulesRepository.MenuItemRefusal(request));
    }

    [Fact]
    public void Portion_names_and_order_type_prices_are_each_unique()
    {
        Assert.Contains("Two portions", RestaurantMenuRulesRepository.MenuItemRefusal(Dish(new SaveItemVariantRequest { Name = "Half" }, new() { Name = " half " })));

        SaveMenuItemRequest request = Dish(new SaveItemVariantRequest { Name = "Half", Price = 100m });
        request.Prices.Add(new() { OrderType = OrderType.Delivery, VariantName = "Half", Price = 110m });
        request.Prices.Add(new() { OrderType = OrderType.Delivery, VariantName = "HALF", Price = 120m });
        Assert.Contains("two delivery prices", RestaurantMenuRulesRepository.MenuItemRefusal(request));

        request.Prices.RemoveAt(1);
        request.Prices.Add(new() { OrderType = OrderType.Takeaway, VariantName = "Quarter", Price = 60m });
        Assert.Contains("Quarter", RestaurantMenuRulesRepository.MenuItemRefusal(request));
    }

    [Fact]
    public void A_choice_group_must_offer_what_it_asks_for()
    {
        SaveMenuItemRequest request = Dish();
        request.ModifierGroups.Add(new SaveModifierGroupRequest
        {
            Name = "Sides",
            MinChoices = 2,
            MaxChoices = 2,
            Modifiers = [new() { Name = "Fries" }],
        });

        Assert.Equal("Sides asks for 2 choices but offers 1.", RestaurantMenuRulesRepository.MenuItemRefusal(request));

        request.ModifierGroups[0].MinChoices = 0;
        request.ModifierGroups[0].Modifiers.Add(new() { Name = "Extra cheese", LinkedItemId = 5 });
        Assert.Contains("needs both the item and the quantity", RestaurantMenuRulesRepository.MenuItemRefusal(request));

        request.ModifierGroups[0].Modifiers[1].LinkedQuantity = 0.03m;
        Assert.Null(RestaurantMenuRulesRepository.MenuItemRefusal(request));
    }
}

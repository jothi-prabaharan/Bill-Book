using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Inventory.Repository.Repositories;
using Shared.Kernel.Restaurant;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// The restaurant menu through its repository (TK-144). Done when: a half
/// portion lists at its own price and a sold-out item cannot be ordered.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class RestaurantMenuTests
{
    private static readonly DateTime Lunchtime = new(2026, 10, 5, 13, 0, 0);

    private readonly PostgresFixture _postgres;

    public RestaurantMenuTests(PostgresFixture postgres) => _postgres = postgres;

    private static async Task<long> SeedItemAsync(InventoryDbContext db, Guid orgId, string code, string name, decimal? price)
    {
        var uomType = new UomType { OrgId = orgId, UomTypeName = $"Count {code}", UomTypeSystemName = $"COUNT_{code}", IsActive = true };
        db.UomTypes.Add(uomType);
        await db.SaveChangesAsync();

        var uom = new UnitOfMeasure
        {
            OrgId = orgId,
            UomTypeId = uomType.UomTypeId,
            UomCode = $"P{code}",
            UomName = $"Plate {code}",
            ConversionToBase = 1m,
            IsBaseUnit = true,
            IsActive = true,
        };
        db.UnitOfMeasures.Add(uom);
        await db.SaveChangesAsync();

        var item = new Item
        {
            OrgId = orgId,
            ItemCode = code,
            ItemName = name,
            ItemProfile = ItemProfile.Restaurant,
            ItemType = ItemType.Goods,
            TrackInventory = false,
            UomTypeId = uomType.UomTypeId,
            InventoryUomId = uom.UomId,
            SalesUomId = uom.UomId,
            PurchaseUomId = uom.UomId,
            ReportUomId = uom.UomId,
            SalesPrice = price,
            IsActive = true,
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.ItemId;
    }

    private async Task<(Guid Customer, Guid Org, long CategoryId, long ItemId)> ArrangeAsync(decimal? price = 200m)
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(customer, org);
        RestaurantMenuRepository menu = new(db);

        Assert.Equal(RestaurantMenuRepository.SeedCategories.Length, await menu.SeedCategoriesAsync(org, "Restaurant", default));
        long categoryId = (await menu.ListCategoriesAsync(false, default)).Single(c => c.Name == "Main course").MenuCategoryId;
        long itemId = await SeedItemAsync(db, org, "DAL", "Dal makhani", price);
        return (customer, org, categoryId, itemId);
    }

    [Fact]
    public async Task Seeding_adds_categories_once_and_only_for_a_restaurant()
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(customer, org);
        RestaurantMenuRepository menu = new(db);

        Assert.Equal(0, await menu.SeedCategoriesAsync(org, "General", default));
        Assert.Equal(RestaurantMenuRepository.SeedCategories.Length, await menu.SeedCategoriesAsync(org, "Restaurant", default));
        Assert.Equal(0, await menu.SeedCategoriesAsync(org, "Restaurant", default));
    }

    [Fact]
    public async Task A_half_portion_lists_and_prices_at_its_own_price()
    {
        var (customer, org, categoryId, itemId) = await ArrangeAsync();
        await using InventoryDbContext db = _postgres.CreateContext(customer, org);
        RestaurantMenuRepository menu = new(db);

        MenuResult<long> saved = await menu.SaveItemAsync(null, new SaveMenuItemRequest
        {
            ItemId = itemId,
            MenuCategoryId = categoryId,
            Variants =
            [
                new() { Name = "Half", Price = 110m, PortionFactor = 0.5m },
                new() { Name = "Full", Price = 200m, PortionFactor = 1m },
            ],
            Prices = [new() { OrderType = OrderType.Delivery, VariantName = "Half", Price = 125m }],
        }, default);
        Assert.Equal(MenuOutcome.Ok, saved.Outcome);

        OrderingMenuView dineIn = await menu.OrderingMenuAsync(OrderType.DineIn, Lunchtime, default);
        OrderingItemView dal = dineIn.Categories.Single().Items.Single();
        Assert.Null(dal.Price);
        Assert.Equal(110m, dal.Variants.Single(v => v.Name == "Half").Price);
        Assert.Equal(200m, dal.Variants.Single(v => v.Name == "Full").Price);

        OrderingMenuView delivery = await menu.OrderingMenuAsync(OrderType.Delivery, Lunchtime, default);
        Assert.Equal(125m, delivery.Categories.Single().Items.Single().Variants.Single(v => v.Name == "Half").Price);

        long halfId = dal.Variants.Single(v => v.Name == "Half").ItemVariantId;
        var (refusal, priced) = await menu.PriceLinesAsync(OrderType.DineIn, Lunchtime, [new() { ItemId = itemId, ItemVariantId = halfId }], default);
        Assert.Null(refusal);
        PricedMenuLine line = Assert.Single(priced!.Lines);
        Assert.Equal(110m, line.UnitPrice);
        Assert.Equal(0.5m, line.PortionFactor);
        Assert.Equal("Half", line.VariantName);

        // A dish sold by portion cannot be ordered without one.
        var (noPortion, _) = await menu.PriceLinesAsync(OrderType.DineIn, Lunchtime, [new() { ItemId = itemId }], default);
        Assert.Equal("Choose a portion of Dal makhani.", noPortion!.Message);
    }

    [Fact]
    public async Task A_sold_out_dish_is_listed_but_cannot_be_ordered()
    {
        var (customer, org, categoryId, itemId) = await ArrangeAsync();
        await using InventoryDbContext db = _postgres.CreateContext(customer, org);
        RestaurantMenuRepository menu = new(db);

        await menu.SaveItemAsync(null, new SaveMenuItemRequest { ItemId = itemId, MenuCategoryId = categoryId }, default);
        Assert.Equal(MenuOutcome.Ok, (await menu.SetItemAvailabilityAsync(itemId, false, default)).Outcome);

        OrderingItemView listed = (await menu.OrderingMenuAsync(OrderType.DineIn, Lunchtime, default)).Categories.Single().Items.Single();
        Assert.False(listed.IsAvailable);
        Assert.Equal(200m, listed.Price);

        var (refusal, priced) = await menu.PriceLinesAsync(OrderType.DineIn, Lunchtime, [new() { ItemId = itemId }], default);
        Assert.Null(priced);
        Assert.Equal(0, refusal!.LineIndex);
        Assert.Equal("Dal makhani is sold out.", refusal.Message);
    }

    [Fact]
    public async Task Choices_add_their_price_and_are_matched_by_id_on_save()
    {
        var (customer, org, categoryId, itemId) = await ArrangeAsync();
        await using InventoryDbContext db = _postgres.CreateContext(customer, org);
        RestaurantMenuRepository menu = new(db);

        SaveMenuItemRequest request = new()
        {
            ItemId = itemId,
            MenuCategoryId = categoryId,
            ModifierGroups =
            [
                new()
                {
                    Name = "Extras",
                    MinChoices = 0,
                    MaxChoices = 2,
                    Modifiers = [new() { Name = "Butter", PriceChange = 20m }, new() { Name = "Cream", PriceChange = 15m }],
                },
            ],
        };
        await menu.SaveItemAsync(null, request, default);

        MenuItemView saved = (await menu.GetItemAsync(itemId, default))!;
        ModifierGroupView extras = saved.ModifierGroups.Single();
        long butter = extras.Modifiers.Single(m => m.Name == "Butter").ModifierId;

        var (_, priced) = await menu.PriceLinesAsync(OrderType.Takeaway, Lunchtime, [new() { ItemId = itemId, ModifierIds = [butter] }], default);
        Assert.Equal(220m, priced!.Lines.Single().UnitPrice);

        // Keeping Butter by id and dropping Cream keeps Butter's id.
        request.ModifierGroups[0].ModifierGroupId = extras.ModifierGroupId;
        request.ModifierGroups[0].Modifiers = [new() { ModifierId = butter, Name = "Butter", PriceChange = 25m }];
        Assert.Equal(MenuOutcome.Ok, (await menu.SaveItemAsync(itemId, request, default)).Outcome);

        ModifierView kept = (await menu.GetItemAsync(itemId, default))!.ModifierGroups.Single().Modifiers.Single();
        Assert.Equal(butter, kept.ModifierId);
        Assert.Equal(25m, kept.PriceChange);
    }

    [Fact]
    public async Task A_dish_off_its_timed_menu_is_neither_listed_nor_priced()
    {
        var (customer, org, categoryId, itemId) = await ArrangeAsync();
        await using InventoryDbContext db = _postgres.CreateContext(customer, org);
        RestaurantMenuRepository menu = new(db);

        long breakfast = (await menu.SaveScheduleAsync(null, new SaveMenuScheduleRequest
        {
            Name = "Breakfast",
            Days = MenuDays.Everyday,
            StartTime = new(7, 0),
            EndTime = new(11, 0),
        }, default)).Value;
        await menu.SaveItemAsync(null, new SaveMenuItemRequest { ItemId = itemId, MenuCategoryId = categoryId, MenuScheduleId = breakfast }, default);

        Assert.Empty((await menu.OrderingMenuAsync(OrderType.DineIn, Lunchtime, default)).Categories);
        var (refusal, _) = await menu.PriceLinesAsync(OrderType.DineIn, Lunchtime, [new() { ItemId = itemId }], default);
        Assert.Equal("Dal makhani is not served at this time.", refusal!.Message);

        Assert.Single((await menu.OrderingMenuAsync(OrderType.DineIn, Lunchtime.Date.AddHours(8), default)).Categories);
    }

    [Fact]
    public async Task A_category_with_dishes_cannot_be_deleted_and_an_item_is_on_the_menu_once()
    {
        var (customer, org, categoryId, itemId) = await ArrangeAsync();
        await using InventoryDbContext db = _postgres.CreateContext(customer, org);
        RestaurantMenuRepository menu = new(db);

        await menu.SaveItemAsync(null, new SaveMenuItemRequest { ItemId = itemId, MenuCategoryId = categoryId }, default);

        Assert.Equal(MenuOutcome.Refused, (await menu.DeleteCategoryAsync(categoryId, default)).Outcome);
        MenuResult<long> again = await menu.SaveItemAsync(null, new SaveMenuItemRequest { ItemId = itemId, MenuCategoryId = categoryId }, default);
        Assert.Equal("That item is already on the menu.", again.Message);
    }

    [Fact]
    public async Task A_dish_nothing_prices_is_left_off_the_order_screen_and_refused()
    {
        var (customer, org, categoryId, itemId) = await ArrangeAsync(price: null);
        await using InventoryDbContext db = _postgres.CreateContext(customer, org);
        RestaurantMenuRepository menu = new(db);

        await menu.SaveItemAsync(null, new SaveMenuItemRequest { ItemId = itemId, MenuCategoryId = categoryId }, default);

        Assert.Empty((await menu.OrderingMenuAsync(OrderType.DineIn, Lunchtime, default)).Categories);
        var (refusal, _) = await menu.PriceLinesAsync(OrderType.DineIn, Lunchtime, [new() { ItemId = itemId }], default);
        Assert.Contains("has no price", refusal!.Message);
    }
}

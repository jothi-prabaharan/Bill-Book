using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Sales.Repository.Repositories;
using Shared.Kernel.Numbering;
using Shared.Kernel.Restaurant;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>The order rules (TK-146), pure.</summary>
public sealed class RestaurantOrderRulesTests
{
    private static RestaurantSettingsView Settings(bool tables = true, RequirementLevel table = RequirementLevel.DineInOnly) =>
        new() { TablesEnabled = tables, TableRequired = tables ? table : RequirementLevel.Off };

    [Fact]
    public void A_takeaway_needs_no_table_when_only_dine_in_does()
    {
        Assert.Null(RestaurantOrderRulesRepository.RequirementRefusal(Settings(), OrderType.Takeaway, null, null, null));
        Assert.Equal("Choose a table for this dine-in order.", RestaurantOrderRulesRepository.RequirementRefusal(Settings(), OrderType.DineIn, null, null, null));
    }

    [Fact]
    public void Always_asks_for_a_table_on_every_order_type()
    {
        Assert.NotNull(RestaurantOrderRulesRepository.RequirementRefusal(Settings(table: RequirementLevel.Always), OrderType.Delivery, null, null, null));
    }

    [Fact]
    public void With_tables_off_no_table_is_kept_or_asked_for()
    {
        RestaurantSettingsView off = new() { TablesEnabled = false, TableRequired = RequirementLevel.Always };

        Assert.Null(RestaurantOrderRulesRepository.RequirementRefusal(off, OrderType.DineIn, null, null, null));
        Assert.Null(RestaurantOrderRulesRepository.EffectiveTable(off, 7));
        Assert.Equal(7, RestaurantOrderRulesRepository.EffectiveTable(Settings(), 7));
    }

    [Fact]
    public void A_customer_and_a_waiter_are_asked_for_by_their_own_settings()
    {
        RestaurantSettingsView strict = new() { CustomerRequired = RequirementLevel.Always, WaiterRequired = true };

        Assert.Equal("Choose the customer for this order.", RestaurantOrderRulesRepository.RequirementRefusal(strict, OrderType.Takeaway, null, null, null));
        Assert.Equal("Choose the waiter taking this order.", RestaurantOrderRulesRepository.RequirementRefusal(strict, OrderType.Takeaway, null, 5, null));
        Assert.Null(RestaurantOrderRulesRepository.RequirementRefusal(strict, OrderType.Takeaway, null, 5, Guid.NewGuid()));
    }

    [Fact]
    public void Cancelled_dishes_are_left_out_of_the_total()
    {
        RestaurantOrderLine[] lines =
        [
            new() { Quantity = 2, UnitPrice = 120m, ItemName = "a" },
            new() { Quantity = 1, UnitPrice = 80m, ItemName = "b", KitchenStatus = KitchenStatus.Cancelled },
        ];

        Assert.Equal(240m, RestaurantOrderRulesRepository.Total(lines));
    }
}

/// <summary>Restaurant orders through their repository (TK-146).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class RestaurantOrderTests
{
    private readonly PostgresFixture _postgres;

    public RestaurantOrderTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record Branch(SalesDbContext Db, RestaurantOrderRepository Orders, RestaurantTableRepository Tables, StubRestaurantSettings Settings, StubRestaurantMenu Menu, long[] TableIds);

    private async Task<Branch> ArrangeAsync(RestaurantSettingsView settings, int tables = 3)
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();
        SalesDbContext db = _postgres.CreateContext(customer, org);
        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(org));
        await db.SaveChangesAsync();

        RestaurantTableRepository tableRepo = new(db);
        await tableRepo.SeedFloorAsync(org, "Restaurant", default);
        long floorId = (await tableRepo.FloorPlanAsync(false, default)).Floors.Single().FloorId;
        long[] ids = new long[tables];
        for (int i = 0; i < tables; i++)
        {
            ids[i] = (await tableRepo.SaveTableAsync(null, new SaveRestaurantTableRequest { FloorId = floorId, Code = $"T{i + 1}" }, default)).Value;
        }

        StubRestaurantSettings stubSettings = new(settings);
        StubRestaurantMenu menu = new();
        RestaurantOrderRepository orders = new(
            db,
            new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
            stubSettings,
            menu,
            new StubUserNames(),
            new StubNameLookup(),
            TimeProvider.System);
        return new Branch(db, orders, tableRepo, stubSettings, menu, ids);
    }

    private static RestaurantSettingsView TablesForDineIn() => new() { TablesEnabled = true, TableRequired = RequirementLevel.DineInOnly };

    private static AddOrderLineRequest Dish(long itemId, decimal quantity = 1, long? variant = null, params long[] modifiers) =>
        new() { ItemId = itemId, Quantity = quantity, ItemVariantId = variant, ModifierIds = [.. modifiers] };

    private static async Task<TableStatus> StatusAsync(Branch b, long tableId) =>
        (await b.Tables.FloorPlanAsync(true, default)).Floors.SelectMany(f => f.Tables).Single(t => t.RestaurantTableId == tableId).Status;

    [Fact]
    public async Task A_takeaway_order_saves_with_no_table()
    {
        Branch b = await ArrangeAsync(TablesForDineIn());
        await using (b.Db)
        {
            OrderResult<long> opened = await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, Lines = [Dish(12, 2)] }, default);

            Assert.Equal(OrderOutcome.Ok, opened.Outcome);
            RestaurantOrderView order = (await b.Orders.GetAsync(opened.Value, default))!;
            Assert.Null(order.RestaurantTableId);
            Assert.StartsWith("ORD/", order.OrderNo);
            Assert.Equal(260m, order.Total); // 2 × (120 + 10 takeaway)
        }
    }

    [Fact]
    public async Task A_dine_in_order_is_refused_without_a_table_only_when_the_setting_requires_it()
    {
        Branch b = await ArrangeAsync(TablesForDineIn());
        await using (b.Db)
        {
            OrderResult<long> refused = await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn }, default);
            Assert.Equal("Choose a table for this dine-in order.", refused.Message);

            b.Settings.Settings = new RestaurantSettingsView { TablesEnabled = true, TableRequired = RequirementLevel.Off };
            Assert.Equal(OrderOutcome.Ok, (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn }, default)).Outcome);

            // Required, but tables switched off: the requirement changes nothing, and a table sent is dropped.
            b.Settings.Settings = new RestaurantSettingsView { TablesEnabled = false, TableRequired = RequirementLevel.Always };
            OrderResult<long> noTables = await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, RestaurantTableId = b.TableIds[0] }, default);
            Assert.Equal(OrderOutcome.Ok, noTables.Outcome);
            Assert.Null((await b.Orders.GetAsync(noTables.Value, default))!.RestaurantTableId);
        }
    }

    [Fact]
    public async Task Opening_at_a_table_occupies_it_and_a_second_order_there_is_refused()
    {
        Branch b = await ArrangeAsync(TablesForDineIn());
        await using (b.Db)
        {
            OrderResult<long> first = await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { RestaurantTableId = b.TableIds[0], Lines = [Dish(5)] }, default);
            Assert.Equal(OrderOutcome.Ok, first.Outcome);
            Assert.Equal(TableStatus.Occupied, await StatusAsync(b, b.TableIds[0]));

            OrderResult<long> second = await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { RestaurantTableId = b.TableIds[0] }, default);
            Assert.Contains("already has order", second.Message);

            // The table cannot be freed under an open order.
            Assert.Contains("Bill or cancel it first", (await b.Tables.SetStatusAsync(b.TableIds[0], TableStatus.Free, default)).Message);
        }
    }

    [Fact]
    public async Task Moving_a_table_moves_its_open_order()
    {
        Branch b = await ArrangeAsync(TablesForDineIn());
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { RestaurantTableId = b.TableIds[0], Lines = [Dish(5)] }, default)).Value;

            Assert.Equal(OrderOutcome.Ok, (await b.Orders.MoveToTableAsync(orderId, b.TableIds[1], default)).Outcome);

            Assert.Equal(b.TableIds[1], (await b.Orders.GetAsync(orderId, default))!.RestaurantTableId);
            Assert.Equal(TableStatus.Free, await StatusAsync(b, b.TableIds[0]));
            Assert.Equal(TableStatus.Occupied, await StatusAsync(b, b.TableIds[1]));
        }
    }

    [Fact]
    public async Task Merging_tables_puts_their_orders_on_the_head_tables_order()
    {
        Branch b = await ArrangeAsync(TablesForDineIn());
        await using (b.Db)
        {
            long head = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { RestaurantTableId = b.TableIds[0], Lines = [Dish(5)] }, default)).Value;
            long other = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { RestaurantTableId = b.TableIds[1], Lines = [Dish(7), Dish(8)] }, default)).Value;

            Assert.Equal(TableOutcome.Ok, (await b.Tables.MergeAsync(b.TableIds[0], [b.TableIds[1]], default)).Outcome);

            RestaurantOrderView merged = (await b.Orders.GetAsync(head, default))!;
            Assert.Equal([1, 2, 3], merged.Lines.Select(l => l.LineNo));
            Assert.Equal(50m + 70m + 80m, merged.Total);

            RestaurantOrderView emptied = (await b.Orders.GetAsync(other, default))!;
            Assert.Equal(RestaurantOrderStatus.Merged, emptied.Status);
            Assert.Empty(emptied.Lines);
        }
    }

    [Fact]
    public async Task A_sold_out_dish_cannot_be_added()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            OrderResult<long> opened = await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, Lines = [Dish(5), Dish(StubRestaurantMenu.SoldOut)] }, default);
            Assert.Equal(OrderOutcome.Refused, opened.Outcome);
            Assert.Equal("Biryani is sold out.", opened.Message);
            Assert.False(await b.Db.RestaurantOrders.AnyAsync());
        }
    }

    [Fact]
    public async Task Without_the_menu_or_the_settings_the_order_waits_rather_than_saving_wrong()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            b.Menu.Down = true;
            Assert.Equal(OrderOutcome.Unavailable, (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, Lines = [Dish(5)] }, default)).Outcome);

            b.Settings.Settings = null;
            Assert.Equal(OrderOutcome.Unavailable, (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway }, default)).Outcome);
        }
    }

    [Fact]
    public async Task Changing_the_order_type_prices_every_dish_again()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, Lines = [Dish(12)] }, default)).Value;
            Assert.Equal(120m, (await b.Orders.GetAsync(orderId, default))!.Total);

            Assert.Equal(OrderOutcome.Ok, (await b.Orders.ChangeOrderTypeAsync(orderId, OrderType.Takeaway, default)).Outcome);
            Assert.Equal(130m, (await b.Orders.GetAsync(orderId, default))!.Total);
        }
    }

    [Fact]
    public async Task A_dish_sent_to_the_kitchen_cannot_be_changed_here()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, Lines = [Dish(5)] }, default)).Value;
            RestaurantOrderLine line = await b.Db.RestaurantOrderLines.SingleAsync();
            line.KitchenStatus = KitchenStatus.Sent;
            await b.Db.SaveChangesAsync();

            OrderResult<bool> removed = await b.Orders.RemoveLineAsync(orderId, line.RestaurantOrderLineId, default);
            Assert.Contains("Cancel it on the kitchen ticket", removed.Message);
        }
    }

    [Fact]
    public async Task Cancelling_needs_a_reason_frees_the_table_and_cancels_what_the_kitchen_has()
    {
        Branch b = await ArrangeAsync(TablesForDineIn());
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { RestaurantTableId = b.TableIds[0], Lines = [Dish(5), Dish(6)] }, default)).Value;
            RestaurantOrderLine sent = await b.Db.RestaurantOrderLines.OrderBy(l => l.LineNo).FirstAsync();
            sent.KitchenStatus = KitchenStatus.Sent;
            await b.Db.SaveChangesAsync();

            Assert.Equal(OrderOutcome.Refused, (await b.Orders.CancelAsync(orderId, " ", default)).Outcome);
            Assert.Equal(OrderOutcome.Ok, (await b.Orders.CancelAsync(orderId, "Guests left", default)).Outcome);

            RestaurantOrderView cancelled = (await b.Orders.GetAsync(orderId, default))!;
            Assert.Equal(RestaurantOrderStatus.Cancelled, cancelled.Status);
            Assert.Equal("Guests left", cancelled.Lines.Single(l => l.LineNo == 1).CancelReason);
            Assert.Equal(TableStatus.Free, await StatusAsync(b, b.TableIds[0]));
        }
    }

    [Fact]
    public async Task A_waiter_must_be_on_the_branch_staff()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView { WaiterRequired = true });
        await using (b.Db)
        {
            Assert.Equal("Choose the waiter taking this order.", (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway }, default)).Message);
            Assert.Equal(
                "Choose a waiter from this branch's staff.",
                (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, WaiterUserId = StubUserNames.Stranger }, default)).Message);

            OrderResult<long> ok = await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, WaiterUserId = Guid.NewGuid() }, default);
            Assert.StartsWith("Waiter ", (await b.Orders.GetAsync(ok.Value, default))!.WaiterName);
        }
    }
}

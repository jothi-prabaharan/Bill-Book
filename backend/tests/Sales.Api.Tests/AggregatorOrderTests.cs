using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Sales.Repository.Clients;
using Sales.Repository.Repositories;
using Shared.Kernel.Numbering;
using Shared.Kernel.Printing;
using Shared.Kernel.Restaurant;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>Aggregator order intake's pure rules (TK-167).</summary>
public sealed class AggregatorOrderRulesTests
{
    [Theory]
    [InlineData("  4821 ", "4821")]
    [InlineData("ZMT-77", "ZMT-77")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void The_aggregators_order_id_is_trimmed_and_a_blank_one_is_nothing(string? sent, string? expected) =>
        Assert.Equal(expected, AggregatorOrderRulesRepository.CleanExternalId(sent));

    [Fact]
    public void The_note_says_where_the_order_came_from_and_who_it_is_for()
    {
        string note = AggregatorOrderRulesRepository.OrderNote(
            OrderAggregator.Zomato, "4821", " Ravi ", "9876543210", "12 Lake Road", "no onions");

        Assert.Equal("Zomato 4821 · Ravi · 9876543210 · 12 Lake Road · no onions", note);
    }

    [Fact]
    public void A_note_leaves_out_what_was_not_sent()
    {
        Assert.Equal("Swiggy 9", AggregatorOrderRulesRepository.OrderNote(OrderAggregator.Swiggy, "9", null, "  ", "", null));
    }

    [Fact]
    public void A_long_note_is_cut_to_the_column()
    {
        string note = AggregatorOrderRulesRepository.OrderNote(OrderAggregator.Swiggy, "9", null, null, new string('x', 600), null);

        Assert.Equal(AggregatorOrderRulesRepository.NoteLimit, note.Length);
        Assert.StartsWith("Swiggy 9 · ", note);
    }
}

/// <summary>Aggregator order intake through its repository (TK-167).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class AggregatorOrderTests
{
    private readonly PostgresFixture _postgres;

    public AggregatorOrderTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>Intake never prints; the ticket is printed by a person from the kitchen screen.</summary>
    private sealed class NoPrinting : IPrintingClient
    {
        public Task<PrintedDocument> RenderAsync(string documentTypeCode, long? printTemplateId, PrintPayload payload, string? watermark, CancellationToken ct) =>
            throw new InvalidOperationException("Aggregator intake must not print.");
    }

    private sealed record Branch(SalesDbContext Db, AggregatorOrderRepository Aggregators, StubRestaurantMenu Menu);

    private async Task<Branch> ArrangeAsync(RestaurantSettingsView settings)
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();
        SalesDbContext db = _postgres.CreateContext(customer, org);
        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(org));
        await db.SaveChangesAsync();

        NumberGenerator numbering = new(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
        StubRestaurantSettings stubSettings = new(settings);
        StubRestaurantMenu menu = new();
        RestaurantOrderRepository orders = new(db, numbering, stubSettings, menu, new StubUserNames(), new StubNameLookup(), TimeProvider.System);
        KotRepository kots = new(db, numbering, new StubUserNames(), new NoPrinting(), TimeProvider.System);
        return new Branch(db, new AggregatorOrderRepository(db, orders, kots, stubSettings, TimeProvider.System), menu);
    }

    private static ReceiveAggregatorOrderRequest Order(string externalId, OrderAggregator aggregator = OrderAggregator.Zomato, params long[] items) => new()
    {
        Aggregator = aggregator,
        ExternalOrderId = externalId,
        CustomerName = "Ravi",
        CustomerPhone = "9876543210",
        DeliveryAddress = "12 Lake Road",
        AggregatorTotal = 340m,
        Lines = [.. (items.Length == 0 ? [5L] : items).Select(id => new AddOrderLineRequest { ItemId = id, Quantity = 1 })],
    };

    [Fact]
    public async Task An_aggregator_order_opens_one_delivery_order_and_remembers_where_it_came_from()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            OrderResult<AggregatorOrderReceipt> received = await b.Aggregators.ReceiveAsync(Order(" 4821 "), default);

            Assert.Equal(OrderOutcome.Ok, received.Outcome);
            Assert.False(received.Value!.AlreadyReceived);
            RestaurantOrder order = await b.Db.RestaurantOrders.Include(o => o.Lines).SingleAsync();
            Assert.Equal(OrderType.Delivery, order.OrderType);
            Assert.Equal(received.Value.RestaurantOrderId, order.RestaurantOrderId);
            Assert.Equal("Zomato 4821 · Ravi · 9876543210 · 12 Lake Road", order.Notes);
            Assert.Single(order.Lines);
            Assert.Equal([OrderType.Delivery], b.Menu.PricedFor);

            AggregatorOrder row = await b.Db.AggregatorOrders.SingleAsync();
            Assert.Equal(OrderAggregator.Zomato, row.Aggregator);
            Assert.Equal("4821", row.ExternalOrderId);
            Assert.Equal(340m, row.AggregatorTotal);
            Assert.Equal(order.RestaurantOrderId, row.RestaurantOrderId);
        }
    }

    [Fact]
    public async Task The_same_aggregator_order_delivered_twice_is_one_order()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            OrderResult<AggregatorOrderReceipt> first = await b.Aggregators.ReceiveAsync(Order("4821"), default);
            OrderResult<AggregatorOrderReceipt> second = await b.Aggregators.ReceiveAsync(Order("4821"), default);

            Assert.Equal(OrderOutcome.Ok, second.Outcome);
            Assert.True(second.Value!.AlreadyReceived);
            Assert.Equal(first.Value!.RestaurantOrderId, second.Value.RestaurantOrderId);
            Assert.Equal(first.Value.OrderNo, second.Value.OrderNo);
            Assert.Equal(1, await b.Db.RestaurantOrders.CountAsync());
            Assert.Equal(1, await b.Db.AggregatorOrders.CountAsync());
        }
    }

    [Fact]
    public async Task The_same_id_from_the_other_aggregator_is_a_different_order()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            await b.Aggregators.ReceiveAsync(Order("77", OrderAggregator.Zomato), default);
            OrderResult<AggregatorOrderReceipt> swiggy = await b.Aggregators.ReceiveAsync(Order("77", OrderAggregator.Swiggy), default);

            Assert.False(swiggy.Value!.AlreadyReceived);
            Assert.Equal(2, await b.Db.RestaurantOrders.CountAsync());
        }
    }

    [Fact]
    public async Task A_branch_that_demands_a_customer_and_a_waiter_still_takes_the_aggregators_order()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView { CustomerRequired = RequirementLevel.Always, WaiterRequired = true });
        await using (b.Db)
        {
            OrderResult<AggregatorOrderReceipt> received = await b.Aggregators.ReceiveAsync(Order("5"), default);

            Assert.Equal(OrderOutcome.Ok, received.Outcome);
            Assert.Null((await b.Db.RestaurantOrders.SingleAsync()).WaiterUserId);
        }
    }

    [Fact]
    public async Task With_the_kitchen_on_the_order_goes_straight_to_it_one_ticket_per_station()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView { KitchenEnabled = true });
        await using (b.Db)
        {
            OrderResult<AggregatorOrderReceipt> received = await b.Aggregators.ReceiveAsync(Order("9", OrderAggregator.Swiggy, 5, 60), default);

            Assert.Equal(2, received.Value!.KotIds.Count);
            Assert.Equal(2, await b.Db.Kots.CountAsync());
            Assert.All(await b.Db.RestaurantOrderLines.ToListAsync(), l => Assert.Equal(KitchenStatus.Sent, l.KitchenStatus));
        }
    }

    [Fact]
    public async Task With_the_kitchen_off_no_ticket_is_made()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView { KitchenEnabled = false });
        await using (b.Db)
        {
            OrderResult<AggregatorOrderReceipt> received = await b.Aggregators.ReceiveAsync(Order("9"), default);

            Assert.Empty(received.Value!.KotIds);
            Assert.Equal(0, await b.Db.Kots.CountAsync());
        }
    }

    [Fact]
    public async Task A_sold_out_dish_refuses_the_order_and_keeps_no_record_of_it()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            OrderResult<AggregatorOrderReceipt> refused = await b.Aggregators.ReceiveAsync(Order("3", OrderAggregator.Zomato, 5, StubRestaurantMenu.SoldOut), default);

            Assert.Equal(OrderOutcome.Refused, refused.Outcome);
            Assert.Equal(0, await b.Db.AggregatorOrders.CountAsync());
            Assert.Equal(0, await b.Db.RestaurantOrders.CountAsync());

            // The aggregator can send it again once the dish is back.
            Assert.Equal(OrderOutcome.Ok, (await b.Aggregators.ReceiveAsync(Order("3"), default)).Outcome);
        }
    }

    [Fact]
    public async Task A_menu_that_cannot_be_reached_answers_unavailable_and_saves_nothing()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            b.Menu.Down = true;

            OrderResult<AggregatorOrderReceipt> down = await b.Aggregators.ReceiveAsync(Order("3"), default);

            Assert.Equal(OrderOutcome.Unavailable, down.Outcome);
            Assert.Equal(0, await b.Db.AggregatorOrders.CountAsync());
        }
    }

    [Fact]
    public async Task A_blank_order_id_is_refused()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            OrderResult<AggregatorOrderReceipt> refused = await b.Aggregators.ReceiveAsync(Order("   "), default);

            Assert.Equal(OrderOutcome.Refused, refused.Outcome);
            Assert.Equal("The aggregator's order id is required.", refused.Message);
        }
    }

    [Fact]
    public async Task The_list_shows_each_received_order_newest_first_and_filters_by_aggregator()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            await b.Aggregators.ReceiveAsync(Order("1", OrderAggregator.Zomato), default);
            await b.Aggregators.ReceiveAsync(Order("2", OrderAggregator.Swiggy), default);

            List<AggregatorOrderView> all = await b.Aggregators.ListAsync(null, null, default);
            List<AggregatorOrderView> swiggy = await b.Aggregators.ListAsync(OrderAggregator.Swiggy, null, default);

            Assert.Equal(["2", "1"], all.Select(a => a.ExternalOrderId));
            Assert.Equal(["2"], swiggy.Select(a => a.ExternalOrderId));
            Assert.All(all, a => Assert.StartsWith("ORD", a.OrderNo));
        }
    }
}

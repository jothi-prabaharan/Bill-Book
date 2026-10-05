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

/// <summary>The kitchen rules (TK-147), pure.</summary>
public sealed class KotRulesTests
{
    [Theory]
    [InlineData(KitchenStatus.Sent, KitchenStatus.Preparing, true)]
    [InlineData(KitchenStatus.Sent, KitchenStatus.Ready, true)]
    [InlineData(KitchenStatus.Preparing, KitchenStatus.Ready, true)]
    [InlineData(KitchenStatus.Ready, KitchenStatus.Served, true)]
    [InlineData(KitchenStatus.Ready, KitchenStatus.Preparing, false)]
    [InlineData(KitchenStatus.Served, KitchenStatus.Ready, false)]
    [InlineData(KitchenStatus.Cancelled, KitchenStatus.Ready, false)]
    [InlineData(KitchenStatus.NotSent, KitchenStatus.Preparing, false)]
    [InlineData(KitchenStatus.Sent, KitchenStatus.Cancelled, false)]
    public void A_dish_moves_forward_through_the_kitchen(KitchenStatus from, KitchenStatus to, bool allowed)
    {
        Assert.Equal(allowed, KotRulesRepository.CanMove(from, to));
    }

    [Fact]
    public void Cancelling_a_started_dish_needs_someone_who_can_approve()
    {
        Assert.Null(KotRulesRepository.CancelRefusal(KitchenStatus.Sent, canApprove: false));
        Assert.NotNull(KotRulesRepository.CancelRefusal(KitchenStatus.Preparing, canApprove: false));
        Assert.Null(KotRulesRepository.CancelRefusal(KitchenStatus.Ready, canApprove: true));
        Assert.NotNull(KotRulesRepository.CancelRefusal(KitchenStatus.Served, canApprove: true));
    }

    [Fact]
    public void With_tickets_required_an_order_with_an_unsent_dish_is_not_billed()
    {
        RestaurantOrderLine[] lines = [new() { ItemName = "a", KitchenStatus = KitchenStatus.Sent }, new() { ItemName = "b", KitchenStatus = KitchenStatus.NotSent }];

        Assert.NotNull(KotRulesRepository.BillingRefusal(new RestaurantSettingsView { KitchenEnabled = true, KotRequired = true }, lines));
        Assert.Null(KotRulesRepository.BillingRefusal(new RestaurantSettingsView { KitchenEnabled = true, KotRequired = false }, lines));
    }
}

/// <summary>Kitchen tickets through their repository (TK-147).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class KitchenTicketTests
{
    private readonly PostgresFixture _postgres;

    public KitchenTicketTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class RecordingPrinting : IPrintingClient
    {
        public List<(string Type, PrintPayload Payload)> Printed { get; } = [];

        public Task<PrintedDocument> RenderAsync(string documentTypeCode, long? printTemplateId, PrintPayload payload, string? watermark, CancellationToken ct)
        {
            Printed.Add((documentTypeCode, payload));
            return Task.FromResult(new PrintedDocument { Html = "<p>ticket</p>", PageCount = 1 });
        }
    }

    private sealed record Kitchen(SalesDbContext Db, RestaurantOrderRepository Orders, KotRepository Kots, RecordingPrinting Printing);

    private async Task<Kitchen> ArrangeAsync()
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();
        SalesDbContext db = _postgres.CreateContext(customer, org);
        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(org));
        await db.SaveChangesAsync();

        NumberGenerator numbering = new(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
        RecordingPrinting printing = new();
        RestaurantOrderRepository orders = new(
            db, numbering, new StubRestaurantSettings(new RestaurantSettingsView { KitchenEnabled = true }), new StubRestaurantMenu(), new StubUserNames(), new StubNameLookup(), TimeProvider.System);
        return new Kitchen(db, orders, new KotRepository(db, numbering, new StubUserNames(), printing, TimeProvider.System), printing);
    }

    private static AddOrderLineRequest Dish(long itemId, decimal quantity = 1) => new() { ItemId = itemId, Quantity = quantity };

    [Fact]
    public async Task Sending_an_order_makes_one_ticket_per_station_and_only_sends_new_dishes()
    {
        Kitchen k = await ArrangeAsync();
        await using (k.Db)
        {
            long orderId = (await k.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, Lines = [Dish(5), Dish(60), Dish(6)] }, default)).Value;

            OrderResult<List<long>> sent = await k.Kots.SendAsync(orderId, default);
            Assert.Equal(2, sent.Value!.Count);

            List<Kot> tickets = await k.Db.Kots.Include(t => t.Lines).OrderBy(t => t.Station).ToListAsync();
            Assert.Equal([MenuStation.Kitchen, MenuStation.Bar], tickets.Select(t => t.Station));
            Assert.Equal(2, tickets[0].Lines.Count);
            Assert.Single(tickets[1].Lines);
            Assert.All(tickets, t => Assert.StartsWith("KOT/", t.KotNo));
            Assert.All(await k.Db.RestaurantOrderLines.ToListAsync(), l => Assert.Equal(KitchenStatus.Sent, l.KitchenStatus));

            Assert.Equal(OrderOutcome.Refused, (await k.Kots.SendAsync(orderId, default)).Outcome);

            await k.Orders.AddLinesAsync(orderId, [Dish(7)], default);
            Assert.Single((await k.Kots.SendAsync(orderId, default)).Value!);
        }
    }

    [Fact]
    public async Task A_second_print_says_reprint()
    {
        Kitchen k = await ArrangeAsync();
        await using (k.Db)
        {
            long orderId = (await k.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, Lines = [Dish(5, 2)] }, default)).Value;
            long kotId = (await k.Kots.SendAsync(orderId, default)).Value!.Single();

            await k.Kots.PrintAsync(kotId, default);
            await k.Kots.PrintAsync(kotId, default);

            Assert.All(k.Printing.Printed, p => Assert.Equal("KOT", p.Type));
            Assert.Equal(string.Empty, k.Printing.Printed[0].Payload.Singles["Kitchen.Copy"]);
            Assert.Equal("REPRINT 1", k.Printing.Printed[1].Payload.Singles["Kitchen.Copy"]);
            IReadOnlyDictionary<string, object?> row = Assert.Single(k.Printing.Printed[0].Payload.Rows("Item"));
            Assert.Equal(2m, row["Item.Quantity"]);
            Assert.Equal("Dish 5", row["Item.ItemName"]);
        }
    }

    [Fact]
    public async Task A_dish_moves_from_sent_to_ready_to_served()
    {
        Kitchen k = await ArrangeAsync();
        await using (k.Db)
        {
            long orderId = (await k.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, Lines = [Dish(5)] }, default)).Value;
            await k.Kots.SendAsync(orderId, default);
            long kotLineId = (await k.Db.KotLines.SingleAsync()).KotLineId;

            Assert.Equal(OrderOutcome.Ok, (await k.Kots.SetStatusAsync(kotLineId, KitchenStatus.Ready, default)).Outcome);
            Assert.Single(await k.Kots.DisplayAsync(MenuStation.Kitchen, default));

            Assert.Equal(OrderOutcome.Ok, (await k.Kots.SetStatusAsync(kotLineId, KitchenStatus.Served, default)).Outcome);
            Assert.Equal(KitchenStatus.Served, (await k.Db.RestaurantOrderLines.SingleAsync()).KitchenStatus);

            // Every dish served: the ticket leaves the display.
            Assert.Empty(await k.Kots.DisplayAsync(null, default));
            Assert.Equal(OrderOutcome.Refused, (await k.Kots.SetStatusAsync(kotLineId, KitchenStatus.Ready, default)).Outcome);
        }
    }

    [Fact]
    public async Task A_cancel_needs_a_reason_and_once_started_someone_who_can_approve()
    {
        Kitchen k = await ArrangeAsync();
        await using (k.Db)
        {
            long orderId = (await k.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, Lines = [Dish(5), Dish(6)] }, default)).Value;
            await k.Kots.SendAsync(orderId, default);
            List<KotLine> lines = await k.Db.KotLines.OrderBy(l => l.KotLineId).ToListAsync();

            Assert.Equal("Say why the dish is cancelled.", (await k.Kots.CancelLineAsync(lines[0].KotLineId, "  ", canApprove: false, default)).Message);
            Assert.Equal(OrderOutcome.Ok, (await k.Kots.CancelLineAsync(lines[0].KotLineId, "Guest changed mind", canApprove: false, default)).Outcome);

            await k.Kots.SetStatusAsync(lines[1].KotLineId, KitchenStatus.Preparing, default);
            Assert.Contains("approve", (await k.Kots.CancelLineAsync(lines[1].KotLineId, "Burnt", canApprove: false, default)).Message);
            Assert.Equal(OrderOutcome.Ok, (await k.Kots.CancelLineAsync(lines[1].KotLineId, "Burnt", canApprove: true, default)).Outcome);

            RestaurantOrderView order = (await k.Orders.GetAsync(orderId, default))!;
            Assert.All(order.Lines, l => Assert.Equal(KitchenStatus.Cancelled, l.KitchenStatus));
            Assert.Equal(0m, order.Total);
        }
    }

    [Fact]
    public async Task Cancelling_the_order_cancels_its_tickets()
    {
        Kitchen k = await ArrangeAsync();
        await using (k.Db)
        {
            long orderId = (await k.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, Lines = [Dish(5)] }, default)).Value;
            await k.Kots.SendAsync(orderId, default);

            await k.Orders.CancelAsync(orderId, "Walked out", default);

            KotLine line = await k.Db.KotLines.SingleAsync();
            Assert.Equal(KitchenStatus.Cancelled, line.Status);
            Assert.Equal("Walked out", line.CancelReason);
            Assert.Empty(await k.Kots.DisplayAsync(null, default));
        }
    }
}

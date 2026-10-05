using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Repository;
using Sales.Repository.Repositories;
using Shared.Kernel.Numbering;
using Shared.Kernel.Restaurant;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>The booking and waiting-list rules (TK-165), pure.</summary>
public sealed class TableReservationRulesTests
{
    private static readonly DateTime Eight = new(2026, 10, 10, 20, 0, 0);

    [Fact]
    public void A_booking_at_eight_for_ninety_minutes_clashes_with_one_at_half_past() =>
        Assert.True(TableReservationRulesRepository.Overlaps(Eight, 90, Eight.AddMinutes(30), 90));

    [Fact]
    public void A_booking_ending_at_eight_does_not_clash_with_one_starting_at_eight()
    {
        Assert.False(TableReservationRulesRepository.Overlaps(Eight.AddMinutes(-90), 90, Eight, 90));
        Assert.False(TableReservationRulesRepository.Overlaps(Eight, 90, Eight.AddMinutes(90), 60));
    }

    [Fact]
    public void A_booking_inside_another_clashes() =>
        Assert.True(TableReservationRulesRepository.Overlaps(Eight, 180, Eight.AddMinutes(60), 30));

    [Theory]
    [InlineData(ReservationStatus.Booked, true)]
    [InlineData(ReservationStatus.Seated, false)]
    [InlineData(ReservationStatus.NoShow, false)]
    [InlineData(ReservationStatus.Cancelled, false)]
    public void Only_a_booked_booking_can_change(ReservationStatus status, bool changeable) =>
        Assert.Equal(changeable, TableReservationRulesRepository.ChangeRefusal(status) is null);

    [Fact]
    public void A_free_table_takes_the_longest_waiting_party_that_fits()
    {
        var waiting = new[]
        {
            new { Name = "Eight", Size = 8, Joined = Eight },
            new { Name = "Two", Size = 2, Joined = Eight.AddMinutes(5) },
            new { Name = "Four", Size = 4, Joined = Eight.AddMinutes(10) },
        };

        var next = TableReservationRulesRepository.NextThatFits(waiting, 4, w => w.Size, w => w.Joined);

        Assert.Equal("Two", next!.Name);
        Assert.Null(TableReservationRulesRepository.NextThatFits(waiting.Where(w => w.Size > 4), 4, w => w.Size, w => w.Joined));
    }

    [Theory]
    [InlineData(" 98765 43210 ", "9876543210")]
    [InlineData("+44 20-7946-0018", "+442079460018")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void A_phone_is_stored_by_the_convention(string? input, string? stored) =>
        Assert.Equal(stored, TableReservationRulesRepository.CleanPhone(input));

    [Fact]
    public void An_order_opened_from_a_booking_says_whose_it_is() =>
        Assert.Equal("Booking: Ravi, 4 · window seat", TableReservationRulesRepository.SeatingNote("Booking", " Ravi ", 4, "window seat"));
}

/// <summary>Bookings and the waiting list through their repositories (TK-165).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class TableReservationTests
{
    private readonly PostgresFixture _postgres;

    public TableReservationTests(PostgresFixture postgres) => _postgres = postgres;

    private static readonly DateTime Eight = DateTime.Today.AddDays(1).AddHours(20);

    private sealed record Branch(
        SalesDbContext Db,
        TableReservationRepository Reservations,
        WaitingListRepository Waiting,
        RestaurantTableRepository Tables,
        long[] TableIds);

    private async Task<Branch> ArrangeAsync(int tables = 2)
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
            ids[i] = (await tableRepo.SaveTableAsync(null, new SaveRestaurantTableRequest { FloorId = floorId, Code = $"T{i + 1}", Seats = 4 }, default)).Value;
        }

        RestaurantOrderRepository orders = new(
            db,
            new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
            new StubRestaurantSettings(new RestaurantSettingsView { TablesEnabled = true, TableRequired = RequirementLevel.DineInOnly }),
            new StubRestaurantMenu(),
            new StubUserNames(),
            new StubNameLookup(),
            TimeProvider.System);
        return new Branch(
            db,
            new TableReservationRepository(db, orders, TimeProvider.System),
            new WaitingListRepository(db, orders, TimeProvider.System),
            tableRepo,
            ids);
    }

    private static SaveReservationRequest Booking(long? tableId, DateTime at, int party = 2, int minutes = 90) => new()
    {
        GuestName = "Ravi",
        Phone = "98765 43210",
        PartySize = party,
        ReservedFor = at,
        DurationMinutes = minutes,
        RestaurantTableId = tableId,
    };

    private static async Task<TableStatus> StatusAsync(Branch b, long tableId) =>
        (await b.Tables.FloorPlanAsync(true, default)).Floors.SelectMany(f => f.Tables).Single(t => t.RestaurantTableId == tableId).Status;

    [Fact]
    public async Task A_table_booked_for_eight_cannot_be_booked_again_at_half_past()
    {
        Branch b = await ArrangeAsync();
        await using (b.Db)
        {
            OrderResult<long> first = await b.Reservations.SaveAsync(null, Booking(b.TableIds[0], Eight), default);
            Assert.Equal(OrderOutcome.Ok, first.Outcome);

            OrderResult<long> second = await b.Reservations.SaveAsync(null, Booking(b.TableIds[0], Eight.AddMinutes(30)), default);
            Assert.Equal(OrderOutcome.Refused, second.Outcome);
            Assert.Contains("booked from 20:00 to 21:30", second.Message);

            // Another table, or the same one once the first booking is over, is fine.
            Assert.Equal(OrderOutcome.Ok, (await b.Reservations.SaveAsync(null, Booking(b.TableIds[1], Eight.AddMinutes(30)), default)).Outcome);
            Assert.Equal(OrderOutcome.Ok, (await b.Reservations.SaveAsync(null, Booking(b.TableIds[0], Eight.AddMinutes(90)), default)).Outcome);
        }
    }

    [Fact]
    public async Task Seating_a_booking_opens_an_order_on_the_table()
    {
        Branch b = await ArrangeAsync();
        await using (b.Db)
        {
            long id = (await b.Reservations.SaveAsync(null, Booking(b.TableIds[0], Eight, party: 3), default)).Value;

            OrderResult<SeatedParty> seated = await b.Reservations.SeatAsync(id, new SeatPartyRequest(), default);

            Assert.Equal(OrderOutcome.Ok, seated.Outcome);
            var order = await b.Db.RestaurantOrders.SingleAsync(o => o.RestaurantOrderId == seated.Value!.RestaurantOrderId);
            Assert.Equal(b.TableIds[0], order.RestaurantTableId);
            Assert.Equal(3, order.Guests);
            Assert.Equal(OrderType.DineIn, order.OrderType);
            Assert.StartsWith("Booking: Ravi, 3", order.Notes);
            Assert.Equal(TableStatus.Occupied, await StatusAsync(b, b.TableIds[0]));

            ReservationView view = (await b.Reservations.ListAsync(DateOnly.FromDateTime(Eight), false, default)).Single();
            Assert.Equal(ReservationStatus.Seated, view.Status);
            Assert.Equal(seated.Value!.RestaurantOrderId, view.RestaurantOrderId);

            // A seated booking is done with.
            Assert.Equal(OrderOutcome.Refused, (await b.Reservations.SeatAsync(id, new SeatPartyRequest(), default)).Outcome);
            Assert.Equal(OrderOutcome.Refused, (await b.Reservations.CancelAsync(id, default)).Outcome);
        }
    }

    [Fact]
    public async Task A_booking_with_no_table_needs_one_to_be_seated()
    {
        Branch b = await ArrangeAsync();
        await using (b.Db)
        {
            long id = (await b.Reservations.SaveAsync(null, Booking(null, Eight), default)).Value;

            OrderResult<SeatedParty> refused = await b.Reservations.SeatAsync(id, new SeatPartyRequest(), default);
            Assert.Equal(OrderOutcome.Refused, refused.Outcome);
            Assert.Equal("Choose the table to seat them at.", refused.Message);

            OrderResult<SeatedParty> seated = await b.Reservations.SeatAsync(id, new SeatPartyRequest { RestaurantTableId = b.TableIds[1] }, default);
            Assert.Equal(b.TableIds[1], seated.Value!.RestaurantTableId);
        }
    }

    [Fact]
    public async Task A_cancelled_or_no_show_booking_frees_its_time()
    {
        Branch b = await ArrangeAsync();
        await using (b.Db)
        {
            long id = (await b.Reservations.SaveAsync(null, Booking(b.TableIds[0], Eight), default)).Value;
            Assert.Equal(OrderOutcome.Ok, (await b.Reservations.NoShowAsync(id, default)).Outcome);

            Assert.Equal(OrderOutcome.Ok, (await b.Reservations.SaveAsync(null, Booking(b.TableIds[0], Eight.AddMinutes(15)), default)).Outcome);
            Assert.Single(await b.Reservations.ListAsync(DateOnly.FromDateTime(Eight), liveOnly: true, default));
        }
    }

    [Fact]
    public async Task Changing_a_booking_does_not_clash_with_itself_and_a_party_too_big_is_refused()
    {
        Branch b = await ArrangeAsync();
        await using (b.Db)
        {
            long id = (await b.Reservations.SaveAsync(null, Booking(b.TableIds[0], Eight), default)).Value;

            Assert.Equal(OrderOutcome.Ok, (await b.Reservations.SaveAsync(id, Booking(b.TableIds[0], Eight.AddMinutes(30)), default)).Outcome);

            OrderResult<long> tooBig = await b.Reservations.SaveAsync(id, Booking(b.TableIds[0], Eight, party: 6), default);
            Assert.Equal(OrderOutcome.Refused, tooBig.Outcome);
            Assert.Contains("seats 4", tooBig.Message);
        }
    }

    [Fact]
    public async Task A_free_table_takes_the_next_waiting_party_that_fits_and_seating_opens_its_order()
    {
        Branch b = await ArrangeAsync();
        await using (b.Db)
        {
            long six = (await b.Waiting.AddAsync(new AddWaitingPartyRequest { GuestName = "Big group", PartySize = 6 }, default)).Value;
            long two = (await b.Waiting.AddAsync(new AddWaitingPartyRequest { GuestName = "Meena", Phone = "98400 12345", PartySize = 2 }, default)).Value;

            OrderResult<WaitingPartyView?> next = await b.Waiting.NextForTableAsync(b.TableIds[0], default);
            Assert.Equal(two, next.Value!.WaitingListEntryId);
            Assert.Equal("9840012345", next.Value.Phone);

            OrderResult<SeatedParty> seated = await b.Waiting.SeatAsync(two, new SeatPartyRequest { RestaurantTableId = b.TableIds[0] }, default);
            Assert.Equal(OrderOutcome.Ok, seated.Outcome);
            Assert.Equal(TableStatus.Occupied, await StatusAsync(b, b.TableIds[0]));

            // The table is taken now, and the party of six still waits for a bigger one.
            Assert.Equal(OrderOutcome.Refused, (await b.Waiting.NextForTableAsync(b.TableIds[0], default)).Outcome);
            Assert.Null((await b.Waiting.NextForTableAsync(b.TableIds[1], default)).Value);
            Assert.Equal([six], (await b.Waiting.ListAsync(false, default)).Select(w => w.WaitingListEntryId));
        }
    }

    [Fact]
    public async Task A_party_that_leaves_comes_off_the_list_and_cannot_be_seated()
    {
        Branch b = await ArrangeAsync();
        await using (b.Db)
        {
            long id = (await b.Waiting.AddAsync(new AddWaitingPartyRequest { GuestName = "Kumar", PartySize = 2 }, default)).Value;

            Assert.Equal(OrderOutcome.Ok, (await b.Waiting.LeaveAsync(id, default)).Outcome);
            Assert.Empty(await b.Waiting.ListAsync(false, default));
            Assert.Single(await b.Waiting.ListAsync(true, default));
            Assert.Equal(OrderOutcome.Refused, (await b.Waiting.SeatAsync(id, new SeatPartyRequest { RestaurantTableId = b.TableIds[0] }, default)).Outcome);
        }
    }
}

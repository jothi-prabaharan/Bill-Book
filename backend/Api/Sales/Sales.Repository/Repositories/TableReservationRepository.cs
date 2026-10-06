using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Shared.Kernel.Persistence;
using Shared.Kernel.Restaurant;

namespace Sales.Repository.Repositories;

/// <summary>
/// Table bookings (TK-165): book, change, cancel, mark a no-show, and seat,
/// which opens an order on the table through
/// <see cref="RestaurantOrderRepository"/>.
///
/// <b>Two bookings for one table may not overlap.</b> The check and the write
/// are serialised per table: a booking first takes the table row's lock with a
/// no-op update, so a second booking for the same table waits for the first
/// to commit and then sees it. Bookings for different tables never wait on
/// each other.
/// </summary>
public sealed class TableReservationRepository
{
    private readonly SalesDbContext _db;
    private readonly RestaurantOrderRepository _orders;
    private readonly TimeProvider _clock;

    public TableReservationRepository(SalesDbContext db, RestaurantOrderRepository orders, TimeProvider clock)
    {
        _db = db;
        _orders = orders;
        _clock = clock;
    }

    private DateTime Now => _clock.GetLocalNow().DateTime;

    /// <summary>One day's bookings, earliest first; the day the floor plan's strip shows when no date is given.</summary>
    public async Task<List<ReservationView>> ListAsync(DateOnly? date, bool liveOnly, CancellationToken ct)
    {
        DateTime dayStart = (date ?? DateOnly.FromDateTime(Now)).ToDateTime(TimeOnly.MinValue);
        DateTime dayEnd = dayStart.AddDays(1);

        return await (
            from r in _db.TableReservations.AsNoTracking()
            join t in _db.RestaurantTables.AsNoTracking() on r.RestaurantTableId equals t.RestaurantTableId into tables
            from t in tables.DefaultIfEmpty()
            where r.ReservedFor >= dayStart && r.ReservedFor < dayEnd
                && (!liveOnly || r.Status == ReservationStatus.Booked)
            orderby r.ReservedFor, r.TableReservationId
            select new ReservationView
            {
                TableReservationId = r.TableReservationId,
                GuestName = r.GuestName,
                Phone = r.Phone,
                PartySize = r.PartySize,
                ReservedFor = r.ReservedFor,
                DurationMinutes = r.DurationMinutes,
                RestaurantTableId = r.RestaurantTableId,
                TableCode = t == null ? null : t.Code,
                Status = r.Status,
                RestaurantOrderId = r.RestaurantOrderId,
                SeatedAt = r.SeatedAt,
                Notes = r.Notes,
            })
            .ToListAsync(ct);
    }

    /// <summary>Books a table, or changes a booking still booked.</summary>
    public async Task<OrderResult<long>> SaveAsync(long? reservationId, SaveReservationRequest request, CancellationToken ct)
    {
        string? phone = TableReservationRulesRepository.CleanPhone(request.Phone);
        if (phone is null)
        {
            return OrderResult<long>.Refuse("Enter a phone number to reach the guest.");
        }

        if (string.IsNullOrWhiteSpace(request.GuestName))
        {
            return OrderResult<long>.Refuse("Enter the guest's name.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        TableReservation? reservation = null;
        if (reservationId is long id)
        {
            reservation = await _db.TableReservations.FirstOrDefaultAsync(r => r.TableReservationId == id, ct);
            if (reservation is null)
            {
                return OrderResult<long>.Missing();
            }

            if (TableReservationRulesRepository.ChangeRefusal(reservation.Status) is string refusal)
            {
                return OrderResult<long>.Refuse(refusal);
            }
        }

        if (request.RestaurantTableId is long tableId
            && await TableRefusalAsync(tableId, request.PartySize, request.ReservedFor, request.DurationMinutes, reservationId, ct) is string clash)
        {
            return OrderResult<long>.Refuse(clash);
        }

        if (reservation is null)
        {
            reservation = new TableReservation();
            _db.TableReservations.Add(reservation);
        }

        reservation.GuestName = request.GuestName.Trim();
        reservation.Phone = phone;
        reservation.PartySize = request.PartySize;
        reservation.ReservedFor = request.ReservedFor;
        reservation.DurationMinutes = request.DurationMinutes;
        reservation.RestaurantTableId = request.RestaurantTableId;
        reservation.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OrderResult<long>.Done(reservation.TableReservationId);
    }

    public Task<OrderResult<long>> CancelAsync(long reservationId, CancellationToken ct) =>
        CloseAsync(reservationId, ReservationStatus.Cancelled, ct);

    public Task<OrderResult<long>> NoShowAsync(long reservationId, CancellationToken ct) =>
        CloseAsync(reservationId, ReservationStatus.NoShow, ct);

    /// <summary>
    /// The party has arrived: opens an order on the booked table, or on the
    /// table the host chose instead, and marks the booking seated. The order
    /// and the booking change together or not at all.
    /// </summary>
    public async Task<OrderResult<SeatedParty>> SeatAsync(long reservationId, SeatPartyRequest request, CancellationToken ct)
    {
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        TableReservation? reservation = await _db.TableReservations.FirstOrDefaultAsync(r => r.TableReservationId == reservationId, ct);
        if (reservation is null)
        {
            return OrderResult<SeatedParty>.Missing();
        }

        if (TableReservationRulesRepository.ChangeRefusal(reservation.Status) is string refusal)
        {
            return OrderResult<SeatedParty>.Refuse(refusal);
        }

        if ((request.RestaurantTableId ?? reservation.RestaurantTableId) is not long tableId)
        {
            return OrderResult<SeatedParty>.Refuse("Choose the table to seat them at.");
        }

        OrderResult<long> opened = await _orders.OpenAsync(new OpenRestaurantOrderRequest
        {
            OrderType = OrderType.DineIn,
            RestaurantTableId = tableId,
            WaiterUserId = request.WaiterUserId,
            Guests = reservation.PartySize,
            Notes = TableReservationRulesRepository.SeatingNote("Booking", reservation.GuestName, reservation.PartySize, reservation.Notes),
        }, ct);
        if (opened.Outcome != OrderOutcome.Ok)
        {
            return new OrderResult<SeatedParty>(opened.Outcome, null, opened.Message);
        }

        reservation.Status = ReservationStatus.Seated;
        reservation.RestaurantTableId = tableId;
        reservation.RestaurantOrderId = opened.Value;
        reservation.SeatedAt = Now;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return OrderResult<SeatedParty>.Done(new SeatedParty { RestaurantOrderId = opened.Value, RestaurantTableId = tableId });
    }

    private async Task<OrderResult<long>> CloseAsync(long reservationId, ReservationStatus to, CancellationToken ct)
    {
        TableReservation? reservation = await _db.TableReservations.FirstOrDefaultAsync(r => r.TableReservationId == reservationId, ct);
        if (reservation is null)
        {
            return OrderResult<long>.Missing();
        }

        if (TableReservationRulesRepository.ChangeRefusal(reservation.Status) is string refusal)
        {
            return OrderResult<long>.Refuse(refusal);
        }

        reservation.Status = to;
        await _db.SaveChangesAsync(ct);
        return OrderResult<long>.Done(reservation.TableReservationId);
    }

    /// <summary>
    /// Why a table cannot take this booking, or null. Takes the table's row
    /// lock first (a no-op update held to the end of the transaction), so two
    /// bookings for one table are checked one after the other.
    /// </summary>
    private async Task<string?> TableRefusalAsync(
        long tableId, int partySize, DateTime start, int minutes, long? exceptReservationId, CancellationToken ct)
    {
        int locked = await _db.RestaurantTables
            .Where(t => t.RestaurantTableId == tableId && t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.DisplayOrder, t => t.DisplayOrder), ct);
        if (locked == 0)
        {
            return "That table does not exist or is switched off.";
        }

        var trackedTable = _db.ChangeTracker.Entries<RestaurantTable>()
            .FirstOrDefault(e => e.Entity.RestaurantTableId == tableId);
        if (trackedTable is not null)
        {
            trackedTable.State = EntityState.Detached;
        }

        var table = await _db.RestaurantTables
            .Where(t => t.RestaurantTableId == tableId)
            .Select(t => new { t.Code, t.Seats })
            .FirstAsync(ct);
        if (partySize > table.Seats)
        {
            return $"Table {table.Code} seats {table.Seats}; choose a bigger table or merge two.";
        }

        // Only bookings near this one can clash: none lasts longer than 12 hours.
        DateTime from = start.AddMinutes(-720);
        DateTime to = start.AddMinutes(minutes);
        var nearby = await _db.TableReservations
            .Where(r => r.RestaurantTableId == tableId
                && r.TableReservationId != (exceptReservationId ?? 0)
                && (r.Status == ReservationStatus.Booked || r.Status == ReservationStatus.Seated)
                && r.ReservedFor > from && r.ReservedFor < to)
            .Select(r => new { r.GuestName, r.ReservedFor, r.DurationMinutes })
            .ToListAsync(ct);

        var clash = nearby.FirstOrDefault(r => TableReservationRulesRepository.Overlaps(start, minutes, r.ReservedFor, r.DurationMinutes));
        return clash is null
            ? null
            : $"Table {table.Code} is booked from {clash.ReservedFor:HH:mm} to {clash.ReservedFor.AddMinutes(clash.DurationMinutes):HH:mm}.";
    }
}

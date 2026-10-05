using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Shared.Kernel.Persistence;
using Shared.Kernel.Restaurant;

namespace Sales.Repository.Repositories;

/// <summary>
/// The waiting list (TK-165): parties who walk in when no table is free. A
/// table that comes free takes the party waiting longest that fits it;
/// seating opens an order on the table through
/// <see cref="RestaurantOrderRepository"/>.
/// </summary>
public sealed class WaitingListRepository
{
    private readonly SalesDbContext _db;
    private readonly RestaurantOrderRepository _orders;
    private readonly TimeProvider _clock;

    public WaitingListRepository(SalesDbContext db, RestaurantOrderRepository orders, TimeProvider clock)
    {
        _db = db;
        _orders = orders;
        _clock = clock;
    }

    private DateTime Now => _clock.GetLocalNow().DateTime;

    /// <summary>Parties still waiting, longest first; with <paramref name="includeClosed"/>, today's seated and departed too.</summary>
    public async Task<List<WaitingPartyView>> ListAsync(bool includeClosed, CancellationToken ct)
    {
        DateTime today = Now.Date;
        return await (
            from w in _db.WaitingListEntries.AsNoTracking()
            join t in _db.RestaurantTables.AsNoTracking() on w.RestaurantTableId equals t.RestaurantTableId into tables
            from t in tables.DefaultIfEmpty()
            where w.Status == WaitingStatus.Waiting || (includeClosed && w.JoinedAt >= today)
            orderby (w.Status == WaitingStatus.Waiting ? 0 : 1), w.JoinedAt
            select new WaitingPartyView
            {
                WaitingListEntryId = w.WaitingListEntryId,
                GuestName = w.GuestName,
                Phone = w.Phone,
                PartySize = w.PartySize,
                JoinedAt = w.JoinedAt,
                Status = w.Status,
                RestaurantTableId = w.RestaurantTableId,
                TableCode = t == null ? null : t.Code,
                RestaurantOrderId = w.RestaurantOrderId,
                ClosedAt = w.ClosedAt,
                Notes = w.Notes,
            })
            .ToListAsync(ct);
    }

    public async Task<OrderResult<long>> AddAsync(AddWaitingPartyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.GuestName))
        {
            return OrderResult<long>.Refuse("Enter the guest's name.");
        }

        WaitingListEntry entry = new()
        {
            GuestName = request.GuestName.Trim(),
            Phone = TableReservationRulesRepository.CleanPhone(request.Phone),
            PartySize = request.PartySize,
            JoinedAt = Now,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };
        _db.WaitingListEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return OrderResult<long>.Done(entry.WaitingListEntryId);
    }

    /// <summary>
    /// The party a table should take now: the one waiting longest that fits
    /// it. Null when nobody waiting fits. Not found for a table that does not
    /// exist; refused for one that is not free.
    /// </summary>
    public async Task<OrderResult<WaitingPartyView?>> NextForTableAsync(long tableId, CancellationToken ct)
    {
        var table = await _db.RestaurantTables
            .Where(t => t.RestaurantTableId == tableId && t.IsActive)
            .Select(t => new { t.Seats, t.Status })
            .FirstOrDefaultAsync(ct);
        if (table is null)
        {
            return OrderResult<WaitingPartyView?>.Missing();
        }

        if (table.Status != TableStatus.Free)
        {
            return OrderResult<WaitingPartyView?>.Refuse("That table is not free yet.");
        }

        List<WaitingPartyView> waiting = await ListAsync(includeClosed: false, ct);
        return OrderResult<WaitingPartyView?>.Done(
            TableReservationRulesRepository.NextThatFits(waiting, table.Seats, w => w.PartySize, w => w.JoinedAt));
    }

    /// <summary>Seats a waiting party at a table: opens the order and takes them off the list, together.</summary>
    public async Task<OrderResult<SeatedParty>> SeatAsync(long entryId, SeatPartyRequest request, CancellationToken ct)
    {
        if (request.RestaurantTableId is not long tableId)
        {
            return OrderResult<SeatedParty>.Refuse("Choose the table to seat them at.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        WaitingListEntry? entry = await _db.WaitingListEntries.FirstOrDefaultAsync(w => w.WaitingListEntryId == entryId, ct);
        if (entry is null)
        {
            return OrderResult<SeatedParty>.Missing();
        }

        if (TableReservationRulesRepository.WaitingRefusal(entry.Status) is string refusal)
        {
            return OrderResult<SeatedParty>.Refuse(refusal);
        }

        OrderResult<long> opened = await _orders.OpenAsync(new OpenRestaurantOrderRequest
        {
            OrderType = OrderType.DineIn,
            RestaurantTableId = tableId,
            WaiterUserId = request.WaiterUserId,
            Guests = entry.PartySize,
            Notes = TableReservationRulesRepository.SeatingNote("Waiting list", entry.GuestName, entry.PartySize, entry.Notes),
        }, ct);
        if (opened.Outcome != OrderOutcome.Ok)
        {
            return new OrderResult<SeatedParty>(opened.Outcome, null, opened.Message);
        }

        entry.Status = WaitingStatus.Seated;
        entry.RestaurantTableId = tableId;
        entry.RestaurantOrderId = opened.Value;
        entry.ClosedAt = Now;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return OrderResult<SeatedParty>.Done(new SeatedParty { RestaurantOrderId = opened.Value, RestaurantTableId = tableId });
    }

    /// <summary>The party gave up waiting.</summary>
    public async Task<OrderResult<long>> LeaveAsync(long entryId, CancellationToken ct)
    {
        WaitingListEntry? entry = await _db.WaitingListEntries.FirstOrDefaultAsync(w => w.WaitingListEntryId == entryId, ct);
        if (entry is null)
        {
            return OrderResult<long>.Missing();
        }

        if (TableReservationRulesRepository.WaitingRefusal(entry.Status) is string refusal)
        {
            return OrderResult<long>.Refuse(refusal);
        }

        entry.Status = WaitingStatus.Left;
        entry.ClosedAt = Now;
        await _db.SaveChangesAsync(ct);
        return OrderResult<long>.Done(entry.WaitingListEntryId);
    }
}

using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository.Clients;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Restaurant;

namespace Sales.Repository.Repositories;

/// <summary>
/// Restaurant orders (TK-146): open one at a table or at the counter, add and
/// change dishes round by round, move it, change its order type, cancel it.
/// Every dish is priced by Inventory's menu when it is added, and every rule
/// the branch's restaurant settings set is applied here, so the page and the
/// API cannot disagree.
/// </summary>
public sealed class RestaurantOrderRepository
{
    /// <summary>The numbering series code for restaurant orders, seeded per branch.</summary>
    public const string SeriesCode = "RST";

    private const string SettingsDown = "The restaurant settings could not be read. Try again in a moment.";
    private const string MenuDown = "The menu could not be reached to price the order. Try again in a moment.";

    private readonly SalesDbContext _db;
    private readonly INumberGenerator _numbering;
    private readonly IRestaurantSettingsProvider _settings;
    private readonly IRestaurantMenuClient _menu;
    private readonly IUserNameLookup _users;
    private readonly IContactNameLookup _contacts;
    private readonly TimeProvider _clock;

    public RestaurantOrderRepository(
        SalesDbContext db,
        INumberGenerator numbering,
        IRestaurantSettingsProvider settings,
        IRestaurantMenuClient menu,
        IUserNameLookup users,
        IContactNameLookup contacts,
        TimeProvider clock)
    {
        _db = db;
        _numbering = numbering;
        _settings = settings;
        _menu = menu;
        _users = users;
        _contacts = contacts;
        _clock = clock;
    }

    private DateTime Now => _clock.GetLocalNow().DateTime;

    // ---- Reading ----

    public async Task<List<RestaurantOrderView>> ListAsync(RestaurantOrderStatus? status, OrderType? orderType, DateOnly? date, CancellationToken ct)
    {
        List<RestaurantOrder> orders = await _db.RestaurantOrders
            .Include(o => o.Lines)
            .Where(o => status == null || o.Status == status)
            .Where(o => orderType == null || o.OrderType == orderType)
            .Where(o => date == null || o.OrderDate == date)
            .OrderByDescending(o => o.OpenedAt)
            .Take(200)
            .AsNoTracking()
            .ToListAsync(ct);

        return await ViewsAsync(orders, withLines: false, ct);
    }

    public async Task<RestaurantOrderView?> GetAsync(long orderId, CancellationToken ct)
    {
        RestaurantOrder? order = await LoadAsync(orderId, ct, tracking: false);
        return order is null ? null : (await ViewsAsync([order], withLines: true, ct))[0];
    }

    // ---- Opening ----

    /// <summary>
    /// Opens an order. <paramref name="fromAggregator"/> is for an order that
    /// arrived from Zomato or Swiggy (TK-167): the aggregator is the customer
    /// and no waiter or table is involved, so the branch's required-field
    /// rules, which are for a person at the till, do not apply to it.
    /// </summary>
    public async Task<OrderResult<long>> OpenAsync(OpenRestaurantOrderRequest request, CancellationToken ct, bool fromAggregator = false)
    {
        if (await _settings.GetSettingsAsync(ct) is not { } settings)
        {
            return OrderResult<long>.Down(SettingsDown);
        }

        long? tableId = RestaurantOrderRulesRepository.EffectiveTable(settings, request.RestaurantTableId);
        if (!fromAggregator
            && RestaurantOrderRulesRepository.RequirementRefusal(settings, request.OrderType, tableId, request.ContactId, request.WaiterUserId) is string missing)
        {
            return OrderResult<long>.Refuse(missing);
        }

        if (await PeopleRefusalAsync(request.WaiterUserId, request.ContactId, ct) is string people)
        {
            return OrderResult<long>.Refuse(people);
        }

        DateTime now = Now;
        (string? priceRefusal, OrderOutcome priceOutcome, List<RestaurantOrderLine> lines) = await PriceAsync(request.OrderType, now, request.Lines, 1, ct);
        if (priceRefusal is not null)
        {
            return new OrderResult<long>(priceOutcome, 0, priceRefusal);
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        RestaurantTable? table = null;
        if (tableId is long id)
        {
            (string? tableRefusal, table) = await SeatableTableAsync(id, ct);
            if (tableRefusal is not null)
            {
                return OrderResult<long>.Refuse(tableRefusal);
            }
        }

        DateOnly date = DateOnly.FromDateTime(now);
        NumberAllocation number = await _numbering.NextAsync(SeriesCode, date, ct);
        RestaurantOrder order = new()
        {
            OrderNo = number.Code,
            NumberingSeriesId = number.NumberingSeriesId,
            OrderDate = date,
            OpenedAt = now,
            OrderType = request.OrderType,
            RestaurantTableId = table?.RestaurantTableId,
            WaiterUserId = request.WaiterUserId,
            ContactId = request.ContactId,
            Guests = request.Guests,
            Notes = Trimmed(request.Notes),
            Status = RestaurantOrderStatus.Open,
            Lines = lines,
        };
        _db.RestaurantOrders.Add(order);

        if (table is not null)
        {
            await OccupyAsync(table.RestaurantTableId, ct);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OrderResult<long>.Done(order.RestaurantOrderId);
    }

    // ---- Lines ----

    public async Task<OrderResult<bool>> AddLinesAsync(long orderId, IReadOnlyList<AddOrderLineRequest> requested, CancellationToken ct)
    {
        RestaurantOrder? order = await LoadAsync(orderId, ct);
        if (order is null)
        {
            return OrderResult<bool>.Missing();
        }

        if (OpenRefusal(order) is string closed)
        {
            return OrderResult<bool>.Refuse(closed);
        }

        int next = order.Lines.Count == 0 ? 1 : order.Lines.Max(l => l.LineNo) + 1;
        (string? refusal, OrderOutcome outcome, List<RestaurantOrderLine> lines) = await PriceAsync(order.OrderType, Now, requested, next, ct);
        if (refusal is not null)
        {
            return new OrderResult<bool>(outcome, false, refusal);
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);
        order.Lines.AddRange(lines);

        // A new round after the bill went out reopens the table.
        if (order.RestaurantTableId is long tableId)
        {
            await OccupyAsync(tableId, ct);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OrderResult<bool>.Done(true);
    }

    public async Task<OrderResult<bool>> UpdateLineAsync(long orderId, long lineId, UpdateOrderLineRequest request, CancellationToken ct)
    {
        RestaurantOrder? order = await LoadAsync(orderId, ct);
        RestaurantOrderLine? line = order?.Lines.FirstOrDefault(l => l.RestaurantOrderLineId == lineId);
        if (order is null || line is null)
        {
            return OrderResult<bool>.Missing();
        }

        if ((OpenRefusal(order) ?? RestaurantOrderRulesRepository.LineLockedRefusal(line)) is string refusal)
        {
            return OrderResult<bool>.Refuse(refusal);
        }

        line.Quantity = request.Quantity;
        line.Note = Trimmed(request.Note);
        await _db.SaveChangesAsync(ct);
        return OrderResult<bool>.Done(true);
    }

    public async Task<OrderResult<bool>> RemoveLineAsync(long orderId, long lineId, CancellationToken ct)
    {
        RestaurantOrder? order = await LoadAsync(orderId, ct);
        RestaurantOrderLine? line = order?.Lines.FirstOrDefault(l => l.RestaurantOrderLineId == lineId);
        if (order is null || line is null)
        {
            return OrderResult<bool>.Missing();
        }

        if ((OpenRefusal(order) ?? RestaurantOrderRulesRepository.LineLockedRefusal(line)) is string refusal)
        {
            return OrderResult<bool>.Refuse(refusal);
        }

        _db.RestaurantOrderLines.Remove(line);
        await _db.SaveChangesAsync(ct);
        return OrderResult<bool>.Done(true);
    }

    // ---- The order itself ----

    public async Task<OrderResult<bool>> UpdateDetailsAsync(long orderId, UpdateOrderDetailsRequest request, CancellationToken ct)
    {
        RestaurantOrder? order = await LoadAsync(orderId, ct);
        if (order is null)
        {
            return OrderResult<bool>.Missing();
        }

        if (OpenRefusal(order) is string closed)
        {
            return OrderResult<bool>.Refuse(closed);
        }

        if (await _settings.GetSettingsAsync(ct) is not { } settings)
        {
            return OrderResult<bool>.Down(SettingsDown);
        }

        string? refusal = RestaurantOrderRulesRepository.RequirementRefusal(settings, order.OrderType, order.RestaurantTableId, request.ContactId, request.WaiterUserId)
            ?? await PeopleRefusalAsync(request.WaiterUserId, request.ContactId, ct);
        if (refusal is not null)
        {
            return OrderResult<bool>.Refuse(refusal);
        }

        order.WaiterUserId = request.WaiterUserId;
        order.ContactId = request.ContactId;
        order.Guests = request.Guests;
        order.Notes = Trimmed(request.Notes);
        await _db.SaveChangesAsync(ct);
        return OrderResult<bool>.Done(true);
    }

    /// <summary>
    /// Changes dine-in to takeaway or delivery, or back. Every line is priced
    /// again for the new order type; a table is required or not by the new type.
    /// </summary>
    public async Task<OrderResult<bool>> ChangeOrderTypeAsync(long orderId, OrderType orderType, CancellationToken ct)
    {
        RestaurantOrder? order = await LoadAsync(orderId, ct);
        if (order is null)
        {
            return OrderResult<bool>.Missing();
        }

        if (OpenRefusal(order) is string closed)
        {
            return OrderResult<bool>.Refuse(closed);
        }

        if (order.OrderType == orderType)
        {
            return OrderResult<bool>.Done(true);
        }

        if (await _settings.GetSettingsAsync(ct) is not { } settings)
        {
            return OrderResult<bool>.Down(SettingsDown);
        }

        if (RestaurantOrderRulesRepository.RequirementRefusal(settings, orderType, order.RestaurantTableId, order.ContactId, order.WaiterUserId) is string missing)
        {
            return OrderResult<bool>.Refuse(missing);
        }

        List<RestaurantOrderLine> live = order.Lines.Where(l => l.KitchenStatus != KitchenStatus.Cancelled).ToList();
        if (live.Count > 0)
        {
            MenuPricing pricing = await _menu.PriceAsync(
                orderType,
                Now,
                live.Select(l => new MenuLineToPrice(l.ItemId, l.ItemVariantId, l.Modifiers.Select(m => m.ModifierId).ToList())).ToList(),
                ct);
            if (pricing.Unavailable)
            {
                return OrderResult<bool>.Down(MenuDown);
            }

            if (pricing.Refusal is not null)
            {
                return OrderResult<bool>.Refuse($"Line {live[pricing.RefusedLine ?? 0].LineNo}: {pricing.Refusal}");
            }

            for (int i = 0; i < live.Count; i++)
            {
                live[i].UnitPrice = pricing.Lines![i].UnitPrice;
            }
        }

        order.OrderType = orderType;
        await _db.SaveChangesAsync(ct);
        return OrderResult<bool>.Done(true);
    }

    /// <summary>
    /// Seats an order at a table. An order already at one moves with its table
    /// (the old table is freed and the new one takes its status); one with no
    /// table is seated at a free one.
    /// </summary>
    public async Task<OrderResult<long>> MoveToTableAsync(long orderId, long tableId, CancellationToken ct)
    {
        RestaurantOrder? order = await LoadAsync(orderId, ct);
        if (order is null)
        {
            return OrderResult<long>.Missing();
        }

        if (OpenRefusal(order) is string closed)
        {
            return OrderResult<long>.Refuse(closed);
        }

        if (await _settings.GetSettingsAsync(ct) is not { } settings)
        {
            return OrderResult<long>.Down(SettingsDown);
        }

        if (!settings.TablesEnabled)
        {
            return OrderResult<long>.Refuse("Tables are switched off for this branch. Turn them on in Settings › Restaurant.");
        }

        if (order.RestaurantTableId is long fromTable)
        {
            TableResult<long> moved = await new RestaurantTableRepository(_db).MoveAsync(fromTable, tableId, ct);
            return moved.Outcome switch
            {
                TableOutcome.Ok => OrderResult<long>.Done(moved.Value),
                TableOutcome.NotFound => OrderResult<long>.Missing(),
                _ => OrderResult<long>.Refuse(moved.Message!),
            };
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);
        (string? refusal, RestaurantTable? table) = await SeatableTableAsync(tableId, ct);
        if (refusal is not null)
        {
            return OrderResult<long>.Refuse(refusal);
        }

        order.RestaurantTableId = table!.RestaurantTableId;
        await OccupyAsync(table.RestaurantTableId, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OrderResult<long>.Done(table.RestaurantTableId);
    }

    /// <summary>
    /// Cancels an order with a reason. Dishes already in the kitchen are
    /// cancelled with the same reason, and the table is free once it has no
    /// other open order.
    /// </summary>
    public async Task<OrderResult<bool>> CancelAsync(long orderId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OrderResult<bool>.Refuse("Say why the order is cancelled.");
        }

        RestaurantOrder? order = await LoadAsync(orderId, ct);
        if (order is null)
        {
            return OrderResult<bool>.Missing();
        }

        if (OpenRefusal(order) is string closed)
        {
            return OrderResult<bool>.Refuse(closed);
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        order.Status = RestaurantOrderStatus.Cancelled;
        order.CancelReason = reason.Trim();
        List<RestaurantOrderLine> inKitchen = order.Lines.Where(l => l.KitchenStatus is not (KitchenStatus.NotSent or KitchenStatus.Cancelled)).ToList();
        foreach (RestaurantOrderLine line in inKitchen)
        {
            line.KitchenStatus = KitchenStatus.Cancelled;
            line.CancelReason = order.CancelReason;
        }

        // The kitchen's tickets say so too, so the kitchen display drops them (TK-147).
        List<long> inKitchenIds = inKitchen.Select(l => l.RestaurantOrderLineId).ToList();
        foreach (KotLine kotLine in await _db.KotLines.Where(k => inKitchenIds.Contains(k.RestaurantOrderLineId)).ToListAsync(ct))
        {
            kotLine.Status = KitchenStatus.Cancelled;
            kotLine.CancelReason = order.CancelReason;
            kotLine.StatusChangedAt = Now;
        }

        if (order.RestaurantTableId is long tableId)
        {
            await ReleaseIfEmptyAsync(tableId, order.RestaurantOrderId, ct);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OrderResult<bool>.Done(true);
    }

    // ---- Helpers ----

    private Task<RestaurantOrder?> LoadAsync(long orderId, CancellationToken ct, bool tracking = true)
    {
        IQueryable<RestaurantOrder> query = _db.RestaurantOrders
            .Include(o => o.Lines.OrderBy(l => l.LineNo))
            .ThenInclude(l => l.Modifiers);
        return (tracking ? query : query.AsNoTracking()).FirstOrDefaultAsync(o => o.RestaurantOrderId == orderId, ct);
    }

    private static string? OpenRefusal(RestaurantOrder order) => order.Status switch
    {
        RestaurantOrderStatus.Open => null,
        RestaurantOrderStatus.Billed => $"Order {order.OrderNo} has been billed.",
        RestaurantOrderStatus.Merged => $"Order {order.OrderNo} was joined to another table's order.",
        _ => $"Order {order.OrderNo} was cancelled.",
    };

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>A waiter must be on the branch's staff and a customer must exist.</summary>
    private async Task<string?> PeopleRefusalAsync(Guid? waiterUserId, long? contactId, CancellationToken ct)
    {
        if (waiterUserId is Guid waiter && !(await _users.ResolveAsync([waiter], ct)).ContainsKey(waiter))
        {
            return "Choose a waiter from this branch's staff.";
        }

        if (contactId is long contact && !(await _contacts.ResolveAsync([contact], ct)).ContainsKey(contact))
        {
            return "That customer does not exist.";
        }

        return null;
    }

    /// <summary>Prices requested dishes on the menu and makes lines of them, numbered from <paramref name="firstLineNo"/>.</summary>
    private async Task<(string? Refusal, OrderOutcome Outcome, List<RestaurantOrderLine> Lines)> PriceAsync(
        OrderType orderType, DateTime at, IReadOnlyList<AddOrderLineRequest> requested, int firstLineNo, CancellationToken ct)
    {
        if (requested.Count == 0)
        {
            return (null, OrderOutcome.Ok, []);
        }

        MenuPricing pricing = await _menu.PriceAsync(
            orderType,
            at,
            requested.Select(r => new MenuLineToPrice(r.ItemId, r.ItemVariantId, r.ModifierIds)).ToList(),
            ct);
        if (pricing.Unavailable)
        {
            return (MenuDown, OrderOutcome.Unavailable, []);
        }

        if (pricing.Refusal is not null)
        {
            return (pricing.Refusal, OrderOutcome.Refused, []);
        }

        List<RestaurantOrderLine> lines = [];
        for (int i = 0; i < requested.Count; i++)
        {
            MenuPricedLine priced = pricing.Lines![i];
            lines.Add(new RestaurantOrderLine
            {
                LineNo = firstLineNo + i,
                ItemId = priced.ItemId,
                ItemName = priced.ItemName,
                ItemVariantId = priced.ItemVariantId,
                VariantName = priced.VariantName,
                PortionFactor = priced.PortionFactor,
                Station = priced.Station,
                Quantity = requested[i].Quantity,
                UnitPrice = priced.UnitPrice,
                Note = Trimmed(requested[i].Note),
                KitchenStatus = KitchenStatus.NotSent,
                TaxGroupId = priced.TaxGroupId,
                UomId = priced.SalesUomId,
                IsPriceInclusive = priced.IsPriceInclusive,
                TaxTreatment = Enum.TryParse(priced.TaxPreference, out Shared.Kernel.Documents.TaxTreatment treatment) ? treatment : Shared.Kernel.Documents.TaxTreatment.Taxable,
                IsStocked = priced.TrackInventory,
                Modifiers = priced.Modifiers.Select(m => new RestaurantOrderLineModifier
                {
                    ModifierId = m.ModifierId,
                    Name = m.Name,
                    PriceChange = m.PriceChange,
                    LinkedItemId = m.LinkedItemId,
                    LinkedQuantity = m.LinkedQuantity,
                }).ToList(),
            });
        }

        return (null, OrderOutcome.Ok, lines);
    }

    /// <summary>
    /// The table an order can be seated at: the one named, or the head of its
    /// group, active, not billed or being cleaned, and with no other open order.
    /// </summary>
    private async Task<(string? Refusal, RestaurantTable? Table)> SeatableTableAsync(long tableId, CancellationToken ct)
    {
        RestaurantTable? table = await _db.RestaurantTables.FirstOrDefaultAsync(t => t.RestaurantTableId == tableId, ct);
        if (table is null || !table.IsActive)
        {
            return ("Choose a table that is in use.", null);
        }

        if (table.MergedIntoTableId is long headId)
        {
            table = await _db.RestaurantTables.FirstAsync(t => t.RestaurantTableId == headId, ct);
        }

        if (table.Status is TableStatus.Billed or TableStatus.Cleaning)
        {
            return ($"Table {table.Code} is {RestaurantTableRulesRepository.StatusLabel(table.Status)}.", null);
        }

        string? open = await _db.RestaurantOrders
            .Where(o => o.RestaurantTableId == table.RestaurantTableId && o.Status == RestaurantOrderStatus.Open)
            .Select(o => o.OrderNo)
            .FirstOrDefaultAsync(ct);
        return open is null
            ? (null, table)
            : ($"Table {table.Code} already has order {open} open. Add to that order instead.", null);
    }

    /// <summary>Marks a table's group occupied: guests have ordered, or ordered again after the bill.</summary>
    private async Task OccupyAsync(long headId, CancellationToken ct)
    {
        foreach (RestaurantTable t in await _db.RestaurantTables.Where(t => t.RestaurantTableId == headId || t.MergedIntoTableId == headId).ToListAsync(ct))
        {
            if (t.Status is TableStatus.Free or TableStatus.Billed)
            {
                t.Status = TableStatus.Occupied;
            }
        }
    }

    /// <summary>Frees a table's group once it has no open order but <paramref name="leavingOrderId"/>.</summary>
    private async Task ReleaseIfEmptyAsync(long headId, long leavingOrderId, CancellationToken ct)
    {
        if (await _db.RestaurantOrders.AnyAsync(o => o.RestaurantTableId == headId && o.Status == RestaurantOrderStatus.Open && o.RestaurantOrderId != leavingOrderId, ct))
        {
            return;
        }

        foreach (RestaurantTable t in await _db.RestaurantTables.Where(t => t.RestaurantTableId == headId || t.MergedIntoTableId == headId).ToListAsync(ct))
        {
            t.Status = TableStatus.Free;
            if (t.RestaurantTableId != headId)
            {
                t.MergedIntoTableId = null;
            }
        }
    }

    private async Task<List<RestaurantOrderView>> ViewsAsync(List<RestaurantOrder> orders, bool withLines, CancellationToken ct)
    {
        List<long> tableIds = orders.Where(o => o.RestaurantTableId is not null).Select(o => o.RestaurantTableId!.Value).Distinct().ToList();
        Dictionary<long, string> tables = await _db.RestaurantTables
            .Where(t => tableIds.Contains(t.RestaurantTableId))
            .ToDictionaryAsync(t => t.RestaurantTableId, t => t.Code, ct);

        // One call each for every waiter and customer on the page (rule 8).
        IReadOnlyDictionary<Guid, UserRef> waiters = await _users.ResolveAsync(
            orders.Where(o => o.WaiterUserId is not null).Select(o => o.WaiterUserId!.Value).Distinct().ToList(), ct);
        IReadOnlyDictionary<long, NamedRef> customers = await _contacts.ResolveAsync(
            orders.Where(o => o.ContactId is not null).Select(o => o.ContactId!.Value).Distinct().ToList(), ct);

        return orders.Select(o => new RestaurantOrderView
        {
            RestaurantOrderId = o.RestaurantOrderId,
            OrderNo = o.OrderNo,
            OrderDate = o.OrderDate,
            OpenedAt = o.OpenedAt,
            OrderType = o.OrderType,
            RestaurantTableId = o.RestaurantTableId,
            TableCode = o.RestaurantTableId is long t ? tables.GetValueOrDefault(t) : null,
            WaiterUserId = o.WaiterUserId,
            WaiterName = o.WaiterUserId is Guid w ? waiters.GetValueOrDefault(w)?.Name : null,
            ContactId = o.ContactId,
            ContactName = o.ContactId is long c ? customers.GetValueOrDefault(c)?.Name : null,
            Guests = o.Guests,
            Status = o.Status,
            Notes = o.Notes,
            CancelReason = o.CancelReason,
            Total = RestaurantOrderRulesRepository.Total(o.Lines),
            LineCount = o.Lines.Count(l => l.KitchenStatus != KitchenStatus.Cancelled),
            UnsentCount = o.Lines.Count(l => l.KitchenStatus == KitchenStatus.NotSent),
            Lines = withLines
                ? o.Lines.OrderBy(l => l.LineNo).Select(l => new RestaurantOrderLineView
                {
                    RestaurantOrderLineId = l.RestaurantOrderLineId,
                    LineNo = l.LineNo,
                    ItemId = l.ItemId,
                    ItemName = l.ItemName,
                    ItemVariantId = l.ItemVariantId,
                    VariantName = l.VariantName,
                    Station = l.Station,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    Amount = Math.Round(l.Quantity * l.UnitPrice, 2),
                    Note = l.Note,
                    KitchenStatus = l.KitchenStatus,
                    CancelReason = l.CancelReason,
                    RestaurantBillId = l.RestaurantBillId,
                    Modifiers = l.Modifiers.Select(m => new RestaurantOrderModifierView { ModifierId = m.ModifierId, Name = m.Name, PriceChange = m.PriceChange }).ToList(),
                }).ToList()
                : [],
        }).ToList();
    }
}

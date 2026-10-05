using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository.Clients;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Printing;
using Shared.Kernel.Restaurant;

namespace Sales.Repository.Repositories;

/// <summary>
/// Kitchen tickets (TK-147): send an order's new dishes as one ticket per
/// station, print and reprint a ticket, move a dish through the kitchen, and
/// cancel one with a reason. The kitchen display reads its tickets from here.
/// </summary>
public sealed class KotRepository
{
    public const string SeriesCode = "KOT";

    private readonly SalesDbContext _db;
    private readonly INumberGenerator _numbering;
    private readonly IUserNameLookup _users;
    private readonly IPrintingClient _printing;
    private readonly TimeProvider _clock;

    public KotRepository(SalesDbContext db, INumberGenerator numbering, IUserNameLookup users, IPrintingClient printing, TimeProvider clock)
    {
        _db = db;
        _numbering = numbering;
        _users = users;
        _printing = printing;
        _clock = clock;
    }

    private DateTime Now => _clock.GetLocalNow().DateTime;

    /// <summary>
    /// Sends every dish on the order the kitchen has not yet seen: one ticket
    /// per station, numbered from the branch's kitchen ticket series. Returns
    /// the tickets made, in station order, for printing.
    /// </summary>
    public async Task<OrderResult<List<long>>> SendAsync(long orderId, CancellationToken ct)
    {
        RestaurantOrder? order = await _db.RestaurantOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.RestaurantOrderId == orderId, ct);
        if (order is null)
        {
            return OrderResult<List<long>>.Missing();
        }

        if (order.Status != RestaurantOrderStatus.Open)
        {
            return OrderResult<List<long>>.Refuse($"Order {order.OrderNo} is not open.");
        }

        List<RestaurantOrderLine> unsent = order.Lines.Where(l => l.KitchenStatus == KitchenStatus.NotSent).OrderBy(l => l.LineNo).ToList();
        if (unsent.Count == 0)
        {
            return OrderResult<List<long>>.Refuse("Every dish on this order has already gone to the kitchen.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        DateTime now = Now;
        List<Kot> tickets = [];
        foreach (IGrouping<MenuStation, RestaurantOrderLine> station in unsent.GroupBy(l => l.Station).OrderBy(g => g.Key))
        {
            NumberAllocation number = await _numbering.NextAsync(SeriesCode, DateOnly.FromDateTime(now), ct);
            Kot kot = new()
            {
                RestaurantOrderId = order.RestaurantOrderId,
                KotNo = number.Code,
                NumberingSeriesId = number.NumberingSeriesId,
                Station = station.Key,
                SentAt = now,
                Lines = station.Select(l => new KotLine
                {
                    RestaurantOrderLineId = l.RestaurantOrderLineId,
                    Quantity = l.Quantity,
                    Status = KitchenStatus.Sent,
                    StatusChangedAt = now,
                }).ToList(),
            };
            tickets.Add(kot);
            _db.Kots.Add(kot);

            foreach (RestaurantOrderLine line in station)
            {
                line.KitchenStatus = KitchenStatus.Sent;
            }
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OrderResult<List<long>>.Done([.. tickets.Select(t => t.KotId)]);
    }

    /// <summary>
    /// Prints a ticket through the branch's kitchen ticket template, on the
    /// paper it is set up for. Every print after the first says REPRINT and
    /// its count, so the kitchen never makes a dish twice from a second copy.
    /// </summary>
    public async Task<PrintedDocument?> PrintAsync(long kotId, CancellationToken ct)
    {
        Kot? kot = await _db.Kots.Include(k => k.Lines).FirstOrDefaultAsync(k => k.KotId == kotId, ct);
        if (kot is null)
        {
            return null;
        }

        RestaurantOrder order = await _db.RestaurantOrders.AsNoTracking().FirstAsync(o => o.RestaurantOrderId == kot.RestaurantOrderId, ct);
        List<long> lineIds = kot.Lines.Select(l => l.RestaurantOrderLineId).ToList();
        List<RestaurantOrderLine> lines = await _db.RestaurantOrderLines.AsNoTracking()
            .Include(l => l.Modifiers)
            .Where(l => lineIds.Contains(l.RestaurantOrderLineId))
            .ToListAsync(ct);
        string? table = order.RestaurantTableId is long tableId
            ? await _db.RestaurantTables.Where(t => t.RestaurantTableId == tableId).Select(t => t.Code).FirstOrDefaultAsync(ct)
            : null;
        string? waiter = order.WaiterUserId is Guid w ? (await _users.ResolveAsync([w], ct)).GetValueOrDefault(w)?.Name : null;

        kot.PrintedCount++;
        await _db.SaveChangesAsync(ct);

        PrintPayload payload = Payload(kot, order, lines, table, waiter);
        return await _printing.RenderAsync(PlaceholderCatalog.KitchenTicketType, null, payload, null, ct);
    }

    /// <summary>The ticket's data under the catalogue's tags. Public so a test can read it without Printing.</summary>
    public static PrintPayload Payload(Kot kot, RestaurantOrder order, IReadOnlyList<RestaurantOrderLine> lines, string? table, string? waiter)
    {
        Dictionary<long, RestaurantOrderLine> byId = lines.ToDictionary(l => l.RestaurantOrderLineId);
        PrintPayload payload = new();
        payload.Singles["Document.No"] = kot.KotNo;
        payload.Singles["Document.Date"] = DateOnly.FromDateTime(kot.SentAt);
        payload.Singles["Kitchen.Station"] = KotRulesRepository.StationLabel(kot.Station);
        payload.Singles["Kitchen.Table"] = table ?? (order.OrderType == OrderType.DineIn ? "Dine-in" : order.OrderType.ToString());
        payload.Singles["Kitchen.OrderNo"] = order.OrderNo;
        payload.Singles["Kitchen.OrderType"] = order.OrderType == OrderType.DineIn ? "Dine-in" : order.OrderType.ToString();
        payload.Singles["Kitchen.Waiter"] = waiter ?? string.Empty;
        payload.Singles["Kitchen.Guests"] = order.Guests;
        payload.Singles["Kitchen.Time"] = kot.SentAt.ToString("HH:mm", CultureInfo.InvariantCulture);
        payload.Singles["Kitchen.Copy"] = kot.PrintedCount > 1 ? $"REPRINT {kot.PrintedCount - 1}" : string.Empty;

        int slNo = 0;
        payload.Lists["Item"] = kot.Lines
            .Where(k => byId.ContainsKey(k.RestaurantOrderLineId))
            .OrderBy(k => byId[k.RestaurantOrderLineId].LineNo)
            .Select(k =>
            {
                RestaurantOrderLine line = byId[k.RestaurantOrderLineId];
                List<string> detail = [.. line.Modifiers.Select(m => m.Name)];
                if (!string.IsNullOrWhiteSpace(line.Note))
                {
                    detail.Add($"Note: {line.Note}");
                }

                if (k.Status == KitchenStatus.Cancelled)
                {
                    detail.Insert(0, $"CANCELLED: {k.CancelReason}");
                }

                return (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Item.SlNo"] = ++slNo,
                    ["Item.ItemName"] = line.VariantName is null ? line.ItemName : $"{line.ItemName} ({line.VariantName})",
                    ["Item.Description"] = string.Join(", ", detail),
                    ["Item.Quantity"] = k.Quantity,
                };
            })
            .ToList();
        return payload;
    }

    /// <summary>Moves a dish to its next step in the kitchen, on its ticket and on the order alike.</summary>
    public async Task<OrderResult<KitchenStatus>> SetStatusAsync(long kotLineId, KitchenStatus status, CancellationToken ct)
    {
        KotLine? kotLine = await _db.KotLines.FirstOrDefaultAsync(l => l.KotLineId == kotLineId, ct);
        if (kotLine is null)
        {
            return OrderResult<KitchenStatus>.Missing();
        }

        if (!KotRulesRepository.CanMove(kotLine.Status, status))
        {
            return OrderResult<KitchenStatus>.Refuse($"A dish that is {Label(kotLine.Status)} cannot become {Label(status)}.");
        }

        RestaurantOrderLine line = await _db.RestaurantOrderLines.FirstAsync(l => l.RestaurantOrderLineId == kotLine.RestaurantOrderLineId, ct);
        kotLine.Status = status;
        kotLine.StatusChangedAt = Now;
        line.KitchenStatus = status;
        await _db.SaveChangesAsync(ct);
        return OrderResult<KitchenStatus>.Done(status);
    }

    /// <summary>
    /// Cancels a dish on its ticket with a reason that is required. Once the
    /// kitchen has started it, only someone who may approve can cancel it.
    /// </summary>
    public async Task<OrderResult<bool>> CancelLineAsync(long kotLineId, string reason, bool canApprove, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OrderResult<bool>.Refuse("Say why the dish is cancelled.");
        }

        KotLine? kotLine = await _db.KotLines.FirstOrDefaultAsync(l => l.KotLineId == kotLineId, ct);
        if (kotLine is null)
        {
            return OrderResult<bool>.Missing();
        }

        if (KotRulesRepository.CancelRefusal(kotLine.Status, canApprove) is string refusal)
        {
            return OrderResult<bool>.Refuse(refusal);
        }

        RestaurantOrderLine line = await _db.RestaurantOrderLines.FirstAsync(l => l.RestaurantOrderLineId == kotLine.RestaurantOrderLineId, ct);
        RestaurantOrder order = await _db.RestaurantOrders.AsNoTracking().FirstAsync(o => o.RestaurantOrderId == line.RestaurantOrderId, ct);
        if (order.Status != RestaurantOrderStatus.Open)
        {
            return OrderResult<bool>.Refuse($"Order {order.OrderNo} is not open.");
        }

        kotLine.Status = KitchenStatus.Cancelled;
        kotLine.CancelReason = reason.Trim();
        kotLine.StatusChangedAt = Now;
        line.KitchenStatus = KitchenStatus.Cancelled;
        line.CancelReason = kotLine.CancelReason;
        await _db.SaveChangesAsync(ct);
        return OrderResult<bool>.Done(true);
    }

    /// <summary>The tickets of one order, newest first: what the order screen lists for reprinting.</summary>
    public Task<List<KotView>> ForOrderAsync(long orderId, CancellationToken ct) =>
        ViewsAsync(_db.Kots.Where(k => k.RestaurantOrderId == orderId).OrderByDescending(k => k.SentAt), includeDone: true, ct);

    /// <summary>
    /// The kitchen display (TK-147): open orders' tickets at a station, oldest
    /// first, holding only dishes still to make or serve. A ticket whose every
    /// dish is served or cancelled drops off.
    /// </summary>
    public Task<List<KotView>> DisplayAsync(MenuStation? station, CancellationToken ct) =>
        ViewsAsync(
            _db.Kots
                .Where(k => station == null || k.Station == station)
                .Where(k => _db.RestaurantOrders.Any(o => o.RestaurantOrderId == k.RestaurantOrderId && o.Status == RestaurantOrderStatus.Open))
                .Where(k => k.Lines.Any(l => l.Status != KitchenStatus.Served && l.Status != KitchenStatus.Cancelled))
                .OrderBy(k => k.SentAt),
            includeDone: false,
            ct);

    private async Task<List<KotView>> ViewsAsync(IQueryable<Kot> query, bool includeDone, CancellationToken ct)
    {
        List<Kot> kots = await query.Include(k => k.Lines).AsNoTracking().Take(200).ToListAsync(ct);
        List<long> orderIds = kots.Select(k => k.RestaurantOrderId).Distinct().ToList();
        Dictionary<long, RestaurantOrder> orders = await _db.RestaurantOrders.AsNoTracking()
            .Where(o => orderIds.Contains(o.RestaurantOrderId))
            .ToDictionaryAsync(o => o.RestaurantOrderId, ct);
        List<long> lineIds = kots.SelectMany(k => k.Lines).Select(l => l.RestaurantOrderLineId).ToList();
        Dictionary<long, RestaurantOrderLine> lines = await _db.RestaurantOrderLines.AsNoTracking()
            .Include(l => l.Modifiers)
            .Where(l => lineIds.Contains(l.RestaurantOrderLineId))
            .ToDictionaryAsync(l => l.RestaurantOrderLineId, ct);
        List<long> tableIds = orders.Values.Where(o => o.RestaurantTableId is not null).Select(o => o.RestaurantTableId!.Value).Distinct().ToList();
        Dictionary<long, string> tables = await _db.RestaurantTables
            .Where(t => tableIds.Contains(t.RestaurantTableId))
            .ToDictionaryAsync(t => t.RestaurantTableId, t => t.Code, ct);
        IReadOnlyDictionary<Guid, UserRef> waiters = await _users.ResolveAsync(
            orders.Values.Where(o => o.WaiterUserId is not null).Select(o => o.WaiterUserId!.Value).Distinct().ToList(), ct);

        return kots.Select(k =>
        {
            RestaurantOrder order = orders[k.RestaurantOrderId];
            return new KotView
            {
                KotId = k.KotId,
                KotNo = k.KotNo,
                RestaurantOrderId = order.RestaurantOrderId,
                OrderNo = order.OrderNo,
                OrderType = order.OrderType,
                TableCode = order.RestaurantTableId is long t ? tables.GetValueOrDefault(t) : null,
                WaiterName = order.WaiterUserId is Guid w ? waiters.GetValueOrDefault(w)?.Name : null,
                Station = k.Station,
                SentAt = k.SentAt,
                PrintedCount = k.PrintedCount,
                Lines = k.Lines
                    .Where(l => includeDone || (l.Status != KitchenStatus.Served && l.Status != KitchenStatus.Cancelled))
                    .Where(l => lines.ContainsKey(l.RestaurantOrderLineId))
                    .OrderBy(l => lines[l.RestaurantOrderLineId].LineNo)
                    .Select(l =>
                    {
                        RestaurantOrderLine line = lines[l.RestaurantOrderLineId];
                        return new KotLineView
                        {
                            KotLineId = l.KotLineId,
                            RestaurantOrderLineId = line.RestaurantOrderLineId,
                            ItemName = line.ItemName,
                            VariantName = line.VariantName,
                            Quantity = l.Quantity,
                            Note = line.Note,
                            Modifiers = [.. line.Modifiers.Select(m => m.Name)],
                            Status = l.Status,
                            CancelReason = l.CancelReason,
                            StatusChangedAt = l.StatusChangedAt,
                        };
                    })
                    .ToList(),
            };
        }).ToList();
    }

    private static string Label(KitchenStatus status) => status switch
    {
        KitchenStatus.NotSent => "not sent",
        _ => status.ToString().ToLowerInvariant(),
    };
}

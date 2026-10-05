using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Restaurant;

namespace Sales.Repository.Repositories;

/// <summary>
/// Order intake from Zomato and Swiggy (TK-167). An aggregator order becomes
/// an ordinary Delivery <see cref="RestaurantOrder"/> through
/// <see cref="RestaurantOrderRepository"/>, so it is priced from the menu and
/// billed like any other, and its kitchen tickets are made through
/// <see cref="KotRepository"/> when the branch uses the kitchen. This
/// repository adds only what is the aggregator's: the order id it arrived
/// under, kept so the same order delivered twice is one order.
/// </summary>
public sealed class AggregatorOrderRepository
{
    private const string SettingsDown = "The restaurant settings could not be read. Try again in a moment.";

    private readonly SalesDbContext _db;
    private readonly RestaurantOrderRepository _orders;
    private readonly KotRepository _kots;
    private readonly IRestaurantSettingsProvider _settings;
    private readonly TimeProvider _clock;

    public AggregatorOrderRepository(
        SalesDbContext db,
        RestaurantOrderRepository orders,
        KotRepository kots,
        IRestaurantSettingsProvider settings,
        TimeProvider clock)
    {
        _db = db;
        _orders = orders;
        _kots = kots;
        _settings = settings;
        _clock = clock;
    }

    public async Task<List<AggregatorOrderView>> ListAsync(OrderAggregator? aggregator, DateOnly? date, CancellationToken ct)
    {
        DateTime? dayStart = date?.ToDateTime(TimeOnly.MinValue);
        DateTime? dayEnd = dayStart?.AddDays(1);

        return await (
            from a in _db.AggregatorOrders.AsNoTracking()
            join o in _db.RestaurantOrders.AsNoTracking() on a.RestaurantOrderId equals o.RestaurantOrderId
            where (aggregator == null || a.Aggregator == aggregator)
                && (dayStart == null || (a.ReceivedAt >= dayStart && a.ReceivedAt < dayEnd))
            orderby a.ReceivedAt descending
            select new AggregatorOrderView
            {
                AggregatorOrderId = a.AggregatorOrderId,
                Aggregator = a.Aggregator,
                ExternalOrderId = a.ExternalOrderId,
                RestaurantOrderId = a.RestaurantOrderId,
                OrderNo = o.OrderNo,
                ReceivedAt = a.ReceivedAt,
                CustomerName = a.CustomerName,
                CustomerPhone = a.CustomerPhone,
                DeliveryAddress = a.DeliveryAddress,
                AggregatorTotal = a.AggregatorTotal,
            })
            .Take(200)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Takes in an aggregator order. A second delivery of the same aggregator
    /// order id changes nothing and answers with the order it made the first
    /// time, flagged as already received, so a retry from the aggregator's
    /// side is safe.
    /// </summary>
    public async Task<OrderResult<AggregatorOrderReceipt>> ReceiveAsync(ReceiveAggregatorOrderRequest request, CancellationToken ct)
    {
        string? externalId = AggregatorOrderRulesRepository.CleanExternalId(request.ExternalOrderId);
        if (externalId is null)
        {
            return OrderResult<AggregatorOrderReceipt>.Refuse("The aggregator's order id is required.");
        }

        AggregatorOrder? existing = await _db.AggregatorOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Aggregator == request.Aggregator && a.ExternalOrderId == externalId, ct);
        if (existing is not null)
        {
            string orderNo = await _db.RestaurantOrders
                .Where(o => o.RestaurantOrderId == existing.RestaurantOrderId)
                .Select(o => o.OrderNo)
                .FirstAsync(ct);
            return OrderResult<AggregatorOrderReceipt>.Done(new AggregatorOrderReceipt
            {
                RestaurantOrderId = existing.RestaurantOrderId,
                OrderNo = orderNo,
                AlreadyReceived = true,
            });
        }

        if (await _settings.GetSettingsAsync(ct) is not { } settings)
        {
            return OrderResult<AggregatorOrderReceipt>.Down(SettingsDown);
        }

        // Everything below is one transaction: the order, its record, its tickets.
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        OrderResult<long> opened = await _orders.OpenAsync(
            new OpenRestaurantOrderRequest
            {
                OrderType = OrderType.Delivery,
                Notes = AggregatorOrderRulesRepository.OrderNote(
                    request.Aggregator, externalId, request.CustomerName, request.CustomerPhone, request.DeliveryAddress, request.Notes),
                Lines = request.Lines,
            },
            ct,
            fromAggregator: true);
        if (opened.Outcome != OrderOutcome.Ok)
        {
            return new OrderResult<AggregatorOrderReceipt>(opened.Outcome, null, opened.Message);
        }

        long orderId = opened.Value;
        _db.AggregatorOrders.Add(new AggregatorOrder
        {
            Aggregator = request.Aggregator,
            ExternalOrderId = externalId,
            RestaurantOrderId = orderId,
            ReceivedAt = _clock.GetLocalNow().DateTime,
            CustomerName = Trimmed(request.CustomerName),
            CustomerPhone = Trimmed(request.CustomerPhone),
            DeliveryAddress = Trimmed(request.DeliveryAddress),
            AggregatorTotal = request.AggregatorTotal,
        });
        await _db.SaveChangesAsync(ct);

        // The kitchen sees the order at once when the branch uses the kitchen.
        List<long> kotIds = [];
        if (settings.KitchenEnabled)
        {
            OrderResult<List<long>> sent = await _kots.SendAsync(orderId, ct);
            if (sent.Outcome != OrderOutcome.Ok)
            {
                return new OrderResult<AggregatorOrderReceipt>(sent.Outcome, null, sent.Message);
            }

            kotIds = sent.Value!;
        }

        string number = await _db.RestaurantOrders
            .Where(o => o.RestaurantOrderId == orderId)
            .Select(o => o.OrderNo)
            .FirstAsync(ct);

        await tx.CommitAsync(ct);
        return OrderResult<AggregatorOrderReceipt>.Done(new AggregatorOrderReceipt
        {
            RestaurantOrderId = orderId,
            OrderNo = number,
            KotIds = kotIds,
        });
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

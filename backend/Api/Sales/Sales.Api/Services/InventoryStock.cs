using System.Net.Http.Json;

namespace Sales.Api.Services;

/// <summary>
/// Holds and releases stock for confirmed orders.
///
/// <b>Sales never touches a quantity itself.</b> Inventory owns the pool and the
/// guarded update that protects it, so this is a call rather than a write —
/// crossing that boundary is what CLAUDE.md's rule 8 forbids, and it is also
/// what would let two services disagree about what is available.
///
/// The tenant travels in the body: the internal door is guarded by the shared
/// key, and forwarding the user's token would mean whoever confirms an order
/// needs inventory permissions to do a sales act.
/// </summary>
public interface IInventoryStock
{
    /// <summary>Takes the lot, or takes none of it and says which lines were short.</summary>
    Task<StockReservationResult> ReserveAsync(
        Guid customerId, Guid orgId, long salesOrderId, IReadOnlyList<StockLine> lines,
        CancellationToken ct);

    /// <summary>Gives stock back. Reported rather than refused — see the implementation.</summary>
    Task<StockReservationResult> ReleaseAsync(
        Guid customerId, Guid orgId, long salesOrderId, IReadOnlyList<StockLine> lines,
        CancellationToken ct);
}

/// <summary>One line's claim, in the item's own stock unit.</summary>
public sealed record StockLine(int LineNumber, long ItemId, decimal Quantity);

public sealed record StockReservationResult(
    bool Ok, IReadOnlyList<StockShortage> Shortages, bool Unreachable = false);

/// <summary>A line Inventory could not satisfy, with enough to say why on screen.</summary>
public sealed record StockShortage(
    int LineNumber,
    long ItemId,
    string ItemCode,
    string ItemName,
    decimal Requested,
    decimal Available,
    string Outcome);

public sealed class InventoryStock : IInventoryStock
{
    private readonly HttpClient _http;
    private readonly ILogger<InventoryStock> _log;

    public InventoryStock(HttpClient http, ILogger<InventoryStock> log)
    {
        _http = http;
        _log = log;
    }

    public Task<StockReservationResult> ReserveAsync(
        Guid customerId, Guid orgId, long salesOrderId, IReadOnlyList<StockLine> lines,
        CancellationToken ct) =>
        CallAsync("internal/stock/reserve", customerId, orgId, salesOrderId, lines, ct);

    public Task<StockReservationResult> ReleaseAsync(
        Guid customerId, Guid orgId, long salesOrderId, IReadOnlyList<StockLine> lines,
        CancellationToken ct) =>
        CallAsync("internal/stock/release", customerId, orgId, salesOrderId, lines, ct);

    private async Task<StockReservationResult> CallAsync(
        string path,
        Guid customerId,
        Guid orgId,
        long salesOrderId,
        IReadOnlyList<StockLine> lines,
        CancellationToken ct)
    {
        var body = new
        {
            customerId,
            orgId,
            sourceType = "SOR",
            sourceId = salesOrderId,
            lines = lines.Select(l => new
            {
                lineNumber = l.LineNumber,
                itemId = l.ItemId,
                quantity = l.Quantity,
            }),
        };

        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync(path, body, ct);

            // A shortage comes back as 400 with the same shape as success, which
            // is the point: the screen needs the numbers, not a message.
            ReservationResponse? payload =
                await response.Content.ReadFromJsonAsync<ReservationResponse>(ct);

            if (payload is null)
            {
                return new StockReservationResult(false, [], Unreachable: true);
            }

            IReadOnlyList<StockShortage> shortages =
            [
                .. payload.Lines
                    .Where(l => !l.Ok)
                    .Select(l => new StockShortage(
                        l.LineNumber, l.ItemId, l.ItemCode ?? string.Empty,
                        l.ItemName ?? string.Empty, l.Requested, l.Available, l.Outcome ?? "Refused")),
            ];

            return new StockReservationResult(payload.Reserved, shortages);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Unreachable is not the same as refused, and the caller must not
            // treat it as one: a confirm that could not reach Inventory has
            // reserved nothing and must leave the order a draft, not fail it.
            _log.LogWarning(
                ex, "Inventory could not be reached to {Path} for order {SalesOrderId}",
                path, salesOrderId);

            return new StockReservationResult(false, [], Unreachable: true);
        }
    }

    private sealed record ReservationResponse(bool Reserved, List<ReservationLine> Lines);

    private sealed record ReservationLine(
        int LineNumber,
        long ItemId,
        string? ItemCode,
        string? ItemName,
        decimal Requested,
        decimal Available,
        bool Ok,
        string? Outcome);
}

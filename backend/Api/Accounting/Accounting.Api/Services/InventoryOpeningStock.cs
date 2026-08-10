using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Services;

/// <summary>
/// Tells Inventory what stock the branch held at go-live.
///
/// <b>Accounting drives the migration but does not own stock.</b> An item's
/// opening quantity and unit cost seed its weighted average, and every cost of
/// sale until the next receipt is computed from that figure — so Inventory
/// records the movement and posts <c>Dr Inventory / Cr Opening Balance Equity</c>
/// itself. Accounting posting a stock value of its own would be a second figure
/// against the Inventory account, free to disagree with the stock the items are
/// actually carrying, and the disagreement would surface as a stock valuation
/// report that does not tie to the balance sheet.
/// </summary>
public interface IInventoryOpeningStock
{
    /// <summary>
    /// Records the branch's opening stock and answers what it came to.
    ///
    /// <b>Safe to call twice.</b> Inventory keys a movement on its source document
    /// and line, so a retried finalize re-presents the same lines and is told each
    /// is already recorded rather than doubling the stock.
    /// </summary>
    Task<OpeningStockOutcome> RecordAsync(
        long openingBalanceId,
        DateOnly asOfDate,
        IReadOnlyList<OpeningStockRequestLine> lines,
        CancellationToken ct);
}

/// <param name="LineNumber">The opening balance line. Inventory's idempotency key, with the document.</param>
public sealed record OpeningStockRequestLine(
    long LineNumber, long ItemId, decimal Quantity, decimal UnitCost, long? WarehouseId);

/// <param name="Value">
/// What the recorded stock came to. Null when nothing was recorded, which is not
/// the same as zero — an empty migration legitimately has no stock.
/// </param>
public sealed record OpeningStockOutcome(bool Recorded, decimal Value, string? Detail = null);

public sealed class InventoryOpeningStock : IInventoryOpeningStock
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;
    private readonly ILogger<InventoryOpeningStock> _log;

    public InventoryOpeningStock(
        HttpClient http, ITenantContext tenant, ILogger<InventoryOpeningStock> log)
    {
        _http = http;
        _tenant = tenant;
        _log = log;
    }

    public async Task<OpeningStockOutcome> RecordAsync(
        long openingBalanceId,
        DateOnly asOfDate,
        IReadOnlyList<OpeningStockRequestLine> lines,
        CancellationToken ct)
    {
        // No stock to bring across is a perfectly ordinary migration — a service
        // business has none — and worth answering without a round trip.
        if (lines.Count == 0)
        {
            return new OpeningStockOutcome(Recorded: true, Value: 0m);
        }

        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return new OpeningStockOutcome(
                Recorded: false, Value: 0m,
                "There is no branch context to record opening stock against.");
        }

        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync(
                "internal/stock/opening",
                new
                {
                    customerId,
                    orgId,
                    openingBalanceId,
                    asOfDate,
                    lines = lines.Select(l => new
                    {
                        lineNumber = l.LineNumber,
                        itemId = l.ItemId,
                        quantity = l.Quantity,
                        unitCost = l.UnitCost,
                        warehouseId = l.WarehouseId,
                    }),
                },
                ct);

            OpeningStockResponseDto? body =
                await response.Content.ReadFromJsonAsync<OpeningStockResponseDto>(ct);

            if (response.IsSuccessStatusCode)
            {
                return new OpeningStockOutcome(Recorded: true, body?.Value ?? 0m);
            }

            // A partial success is the state a finalize must never treat as done,
            // so the refused lines are named rather than summarized.
            string refused = body?.Lines is { Count: > 0 } rows
                ? string.Join(", ", rows
                    .Where(l => !l.Recorded)
                    .Select(l => $"line {l.LineNumber} (item {l.ItemId}): {l.Outcome}"))
                : $"Inventory answered {(int)response.StatusCode}";

            _log.LogWarning(
                "Opening stock for {OpeningBalanceId} was refused: {Refused}",
                openingBalanceId, refused);

            return new OpeningStockOutcome(Recorded: false, body?.Value ?? 0m, refused);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogWarning(
                ex, "Opening stock for {OpeningBalanceId} could not be delivered.", openingBalanceId);

            return new OpeningStockOutcome(
                Recorded: false, Value: 0m,
                "Inventory could not be reached, so no stock was recorded. This can be retried.");
        }
    }

    private sealed record OpeningStockResponseDto(decimal Value, List<OpeningStockLineDto> Lines);

    private sealed record OpeningStockLineDto(
        long LineNumber, long ItemId, bool Recorded, bool AlreadyRecorded, string Outcome);
}

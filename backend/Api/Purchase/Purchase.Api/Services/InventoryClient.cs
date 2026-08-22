using System.Net.Http.Json;

namespace Purchase.Api.Services;

/// <summary>
/// Stock, as Purchase moves it.
///
/// <b>Quantity is synchronous; cost is not.</b> The receipt applies the quantity
/// and opens the cost layer inside this call, because two people receiving the
/// same delivery twice is a real thing and an eventual answer cannot refuse the
/// second one. What settles afterwards is the costing engine's work.
/// </summary>
public interface IInventoryClient
{
    Task<ReceiveStockResponse> ReceiveAsync(ReceiveStockRequest request, CancellationToken ct);

    /// <summary>
    /// Goods going back to a vendor. Inventory answers what the returned units
    /// were carrying, and Purchase credits stock by exactly that — the layer and
    /// the ledger then agree about what left.
    /// </summary>
    Task<ReturnStockResponse> ReturnAsync(ReturnStockRequest request, CancellationToken ct);
}

public sealed class InventoryClient : IInventoryClient
{
    private readonly HttpClient _http;

    public InventoryClient(HttpClient http) => _http = http;

    public async Task<ReceiveStockResponse> ReceiveAsync(
        ReceiveStockRequest request, CancellationToken ct)
    {
        HttpResponseMessage response =
            await _http.PostAsJsonAsync("internal/stock/receipt", request, ct);

        // A 409 carries a body describing which lines were refused, and that is
        // the useful part — it is read rather than thrown away, so the caller can
        // name the line rather than saying "inventory said no".
        ReceiveStockResponse? body = null;
        try
        {
            body = await response.Content.ReadFromJsonAsync<ReceiveStockResponse>(ct);
        }
        catch (Exception)
        {
            // A response that is not the expected shape — a gateway error page,
            // an empty 500 — leaves body null and is reported as a refusal below.
        }

        if (body is not null)
        {
            body.Success = response.IsSuccessStatusCode && body.Success;
            return body;
        }

        return new ReceiveStockResponse
        {
            Success = false,
            Lines = [],
        };
    }

    public async Task<ReturnStockResponse> ReturnAsync(
        ReturnStockRequest request, CancellationToken ct)
    {
        HttpResponseMessage response =
            await _http.PostAsJsonAsync("internal/stock/return", request, ct);

        ReturnStockResponse? body = null;
        try
        {
            body = await response.Content.ReadFromJsonAsync<ReturnStockResponse>(ct);
        }
        catch (Exception)
        {
            // Not the expected shape — reported as a refusal below.
        }

        if (body is not null)
        {
            body.Success = response.IsSuccessStatusCode && body.Success;
            return body;
        }

        return new ReturnStockResponse { Success = false, Lines = [] };
    }
}

/// <summary>Goods going back to a vendor. The mirror of the receipt request.</summary>
public sealed class ReturnStockRequest
{
    public Guid OrgId { get; set; }

    public Guid CustomerId { get; set; }

    public DateOnly MovementDate { get; set; }

    public string SourceType { get; set; } = null!;

    public long SourceId { get; set; }

    public List<ReturnStockLine> Lines { get; set; } = [];
}

public sealed class ReturnStockLine
{
    public long SourceLineId { get; set; }

    public long ItemId { get; set; }

    public decimal Quantity { get; set; }

    public long? WarehouseId { get; set; }

    public long? UomId { get; set; }

    public long? ItemBatchId { get; set; }
}

public sealed class ReturnStockResponse
{
    public bool Success { get; set; }

    /// <summary>What the returned units were carrying — what Inventory is credited by.</summary>
    public decimal TotalValue { get; set; }

    public List<ReturnStockLineResult> Lines { get; set; } = [];
}

public sealed class ReturnStockLineResult
{
    public long SourceLineId { get; set; }

    public long ItemId { get; set; }

    public decimal Quantity { get; set; }

    public bool Success { get; set; }

    public bool AlreadyRecorded { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public decimal UnitCost { get; set; }

    public decimal LineValue { get; set; }
}

/// <summary>Goods arriving against a purchase document. Inventory's contract, mirrored.</summary>
public sealed class ReceiveStockRequest
{
    public Guid OrgId { get; set; }

    public Guid CustomerId { get; set; }

    public DateOnly MovementDate { get; set; }

    /// <summary>The document type — <c>GRN</c>, or <c>BIL</c> when no receipt preceded it.</summary>
    public string SourceType { get; set; } = null!;

    public long SourceId { get; set; }

    public List<ReceiveStockLine> Lines { get; set; } = [];
}

public sealed class ReceiveStockLine
{
    public long SourceLineId { get; set; }

    public long ItemId { get; set; }

    /// <summary>The <b>accepted</b> quantity. A rejected carton never reaches this.</summary>
    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public long? WarehouseId { get; set; }

    public long? UomId { get; set; }

    public long? ItemBatchId { get; set; }

    public string? BatchNumber { get; set; }

    public DateOnly? BatchExpiryDate { get; set; }

    public DateOnly? BatchManufactureDate { get; set; }
}

public sealed class ReceiveStockResponse
{
    public bool Success { get; set; }

    /// <summary>What the received lines came to. What Inventory is debited by.</summary>
    public decimal TotalValue { get; set; }

    public List<ReceiveStockLineResult> Lines { get; set; } = [];
}

public sealed class ReceiveStockLineResult
{
    public long SourceLineId { get; set; }

    public long ItemId { get; set; }

    public decimal Quantity { get; set; }

    public bool Success { get; set; }

    /// <summary>An earlier attempt already recorded it. Still success, and worth nothing new.</summary>
    public bool AlreadyRecorded { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public long? StockMovementId { get; set; }

    public decimal UnitCost { get; set; }

    public decimal LineValue { get; set; }
}

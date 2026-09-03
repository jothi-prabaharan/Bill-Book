using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services;

public interface IInventoryClient
{
    Task<ReserveStockResponse> ReserveAsync(ReserveStockRequest request, CancellationToken ct);
    Task<IssueStockResponse> IssueAsync(IssueStockRequest request, CancellationToken ct);
    Task<ReleaseStockResponse> ReleaseAsync(ReleaseStockRequest request, CancellationToken ct);
    Task<ReceiveStockResponse> ReceiveAsync(ReceiveStockRequest request, CancellationToken ct);

    /// <summary>
    /// What a batch of items can still be promised. Read by the order form so a
    /// line can say what is available before somebody confirms and finds out.
    /// </summary>
    Task<StockAvailabilityResponse> GetAvailabilityAsync(
        StockAvailabilityRequest request, CancellationToken ct);
}

/// <summary>Asks Inventory what a batch of items has available.</summary>
public sealed class StockAvailabilityRequest
{
    public Guid OrgId { get; set; }

    public Guid CustomerId { get; set; }

    public List<long> ItemIds { get; set; } = [];
}

public sealed class StockAvailabilityResponse
{
    public List<StockAvailabilityLine> Lines { get; set; } = [];
}

/// <summary>What one item has, holds, and can still promise.</summary>
public sealed class StockAvailabilityLine
{
    public long ItemId { get; set; }

    public decimal QuantityOnHand { get; set; }

    public decimal QuantityReserved { get; set; }

    public decimal QuantityAvailable { get; set; }

    /// <summary>False when the item is not stocked at all — a service, say.</summary>
    public bool IsTracked { get; set; }
}

public sealed class InventoryClient : IInventoryClient
{
    private readonly HttpClient _http;
    private readonly TenantContext _tenant;

    public InventoryClient(HttpClient http, TenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<ReserveStockResponse> ReserveAsync(ReserveStockRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("internal/stock/reserve", request, ct);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var result = await response.Content.ReadFromJsonAsync<ReserveStockResponse>(cancellationToken: ct);
            return result ?? new ReserveStockResponse { Success = false };
        }
        
        response.EnsureSuccessStatusCode();
        return new ReserveStockResponse { Success = false };
    }

    public async Task<IssueStockResponse> IssueAsync(IssueStockRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("internal/stock/issue", request, ct);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var result = await response.Content.ReadFromJsonAsync<IssueStockResponse>(cancellationToken: ct);
            return result ?? new IssueStockResponse { Success = false };
        }
        
        response.EnsureSuccessStatusCode();
        return new IssueStockResponse { Success = false };
    }

    public async Task<ReleaseStockResponse> ReleaseAsync(ReleaseStockRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("internal/stock/release", request, ct);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var result = await response.Content.ReadFromJsonAsync<ReleaseStockResponse>(cancellationToken: ct);
            return result ?? new ReleaseStockResponse { Success = false };
        }
        
        response.EnsureSuccessStatusCode();
        return new ReleaseStockResponse { Success = false };
    }

    public async Task<ReceiveStockResponse> ReceiveAsync(ReceiveStockRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("internal/stock/receipt", request, ct);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var result = await response.Content.ReadFromJsonAsync<ReceiveStockResponse>(cancellationToken: ct);
            return result ?? new ReceiveStockResponse { Success = false };
        }
        
        response.EnsureSuccessStatusCode();
        return new ReceiveStockResponse { Success = false };
    }

    /// <summary>
    /// What a batch of items can still be promised.
    ///
    /// <b>An unreachable Inventory answers empty rather than throwing.</b> This
    /// is an advisory figure on a form — the authoritative check is the guarded
    /// reservation taken when the order is confirmed, which cannot be fooled by
    /// a stale screen. A form that refused to open because an advisory read
    /// failed would be worse than one that shows no availability.
    /// </summary>
    public async Task<StockAvailabilityResponse> GetAvailabilityAsync(
        StockAvailabilityRequest request, CancellationToken ct)
    {
        if (request.ItemIds.Count == 0)
        {
            return new StockAvailabilityResponse();
        }

        try
        {
            var response = await _http.PostAsJsonAsync("internal/stock/availability", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                return new StockAvailabilityResponse();
            }

            return await response.Content.ReadFromJsonAsync<StockAvailabilityResponse>(
                cancellationToken: ct) ?? new StockAvailabilityResponse();
        }
        catch (HttpRequestException)
        {
            return new StockAvailabilityResponse();
        }
        catch (TaskCanceledException)
        {
            return new StockAvailabilityResponse();
        }
    }
}

// Request and Response shapes expected by the InternalStockController
public sealed record ReserveStockRequest
{
    public Guid OrgId { get; init; }
    public Guid CustomerId { get; init; }
    public List<ReserveStockLine> Lines { get; init; } = [];
}

public sealed record ReserveStockLine
{
    public long ItemId { get; init; }
    public decimal Quantity { get; init; }
}

public sealed record ReserveStockResponse
{
    public bool Success { get; set; }
    public List<ReserveStockLineResult> Lines { get; set; } = [];
}

public sealed record ReserveStockLineResult
{
    public long ItemId { get; init; }
    public decimal RequestedQuantity { get; init; }
    public bool Success { get; init; }
    public string Outcome { get; init; } = string.Empty;
}

public sealed record ReleaseStockRequest
{
    public Guid OrgId { get; init; }
    public Guid CustomerId { get; init; }
    public List<ReleaseStockLine> Lines { get; init; } = [];
}

public sealed record ReleaseStockLine
{
    public long ItemId { get; init; }
    public decimal Quantity { get; init; }
}

public sealed record ReleaseStockResponse
{
    public bool Success { get; set; }
    public List<ReleaseStockLineResult> Lines { get; set; } = [];
}

public sealed record ReleaseStockLineResult
{
    public long ItemId { get; init; }
    public decimal RequestedQuantity { get; init; }
    public bool Success { get; init; }
    public string Outcome { get; init; } = string.Empty;
}

public sealed record IssueStockRequest
{
    public Guid OrgId { get; init; }
    public Guid CustomerId { get; init; }
    public DateOnly MovementDate { get; init; }
    public string SourceType { get; init; } = null!;
    public long SourceId { get; init; }
    public List<IssueStockLine> Lines { get; init; } = [];
}

public sealed record IssueStockLine
{
    public long SourceLineId { get; init; }
    public long ItemId { get; init; }
    public decimal Quantity { get; init; }
    public long? WarehouseId { get; init; }
    public bool ReleaseReservation { get; init; }
}

public sealed record IssueStockResponse
{
    public bool Success { get; set; }
    public decimal TotalValue { get; set; }
    public List<IssueStockLineResult> Lines { get; set; } = [];
}

public sealed record IssueStockLineResult
{
    public long SourceLineId { get; init; }
    public long ItemId { get; init; }
    public decimal RequestedQuantity { get; init; }
    public bool Success { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public long? StockMovementId { get; init; }
    public decimal UnitCost { get; init; }
    public decimal LineValue { get; init; }
}

public sealed record ReceiveStockRequest
{
    public Guid OrgId { get; init; }
    public Guid CustomerId { get; init; }
    public DateOnly MovementDate { get; init; }
    public string SourceType { get; init; } = null!;
    public long SourceId { get; init; }
    public List<ReceiveStockLine> Lines { get; init; } = [];
}

public sealed record ReceiveStockLine
{
    public long SourceLineId { get; init; }
    public long ItemId { get; init; }
    public decimal Quantity { get; init; }
    public long? WarehouseId { get; init; }
    public decimal UnitCost { get; init; }
    public long? ReturnsStockMovementId { get; init; }
}

public sealed record ReceiveStockResponse
{
    public bool Success { get; set; }
    public decimal TotalValue { get; set; }
    public List<ReceiveStockLineResult> Lines { get; set; } = [];
}

public sealed record ReceiveStockLineResult
{
    public long SourceLineId { get; init; }
    public long ItemId { get; init; }
    public decimal Quantity { get; init; }
    public bool Success { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public long? StockMovementId { get; init; }
}

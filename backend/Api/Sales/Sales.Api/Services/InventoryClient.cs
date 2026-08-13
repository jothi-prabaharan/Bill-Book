using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services;

public interface IInventoryClient
{
    Task<ReserveStockResponse> ReserveAsync(ReserveStockRequest request, CancellationToken ct);
    Task<ReleaseStockResponse> ReleaseAsync(ReleaseStockRequest request, CancellationToken ct);
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

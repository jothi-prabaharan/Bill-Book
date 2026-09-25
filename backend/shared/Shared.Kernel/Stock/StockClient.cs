using System.Net;
using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Stock;

// What a service outside Inventory may do with stock (School work orders,
// TK-66): find an item, list the warehouses, and issue parts against its own
// document. The issue goes through Inventory's guarded decrement and is keyed on
// the document and line, so issuing the same line twice moves stock once.

public sealed class StockItemSearchRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public string? Search { get; set; }
}

public sealed class StockItem
{
    public long ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public decimal QuantityOnHand { get; set; }
}

public sealed class StockWarehouse
{
    public long WarehouseId { get; set; }

    public string WarehouseCode { get; set; } = null!;

    public string WarehouseName { get; set; } = null!;
}

public sealed class StockIssueRequest
{
    public Guid OrgId { get; set; }

    public Guid CustomerId { get; set; }

    public DateOnly MovementDate { get; set; }

    /// <summary>The document's transaction type code, e.g. <c>WRK</c>. The issue posts under it.</summary>
    public string SourceType { get; set; } = null!;

    public long SourceId { get; set; }

    public List<StockIssueLine> Lines { get; set; } = [];
}

public sealed class StockIssueLine
{
    public long SourceLineId { get; set; }

    public long ItemId { get; set; }

    public decimal Quantity { get; set; }

    public long? WarehouseId { get; set; }
}

public sealed class StockIssueResponse
{
    public bool Success { get; set; }

    public decimal TotalValue { get; set; }

    public List<StockIssueLineResult> Lines { get; set; } = [];
}

public sealed class StockIssueLineResult
{
    public long SourceLineId { get; set; }

    public bool Success { get; set; }

    /// <summary>Inventory's outcome, e.g. <c>InsufficientStock</c>.</summary>
    public string Outcome { get; set; } = string.Empty;

    public decimal UnitCost { get; set; }
}

public interface IStockClient
{
    Task<IReadOnlyList<StockItem>> SearchAsync(string? search, CancellationToken ct);

    Task<IReadOnlyList<StockWarehouse>> WarehousesAsync(CancellationToken ct);

    /// <summary>Issues, or answers Success false with each line's outcome. Throws when Inventory cannot be reached.</summary>
    Task<StockIssueResponse> IssueAsync(StockIssueRequest request, CancellationToken ct);
}

public sealed class HttpStockClient : IStockClient
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpStockClient(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<StockItem>> SearchAsync(string? search, CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/items/search", new StockItemSearchRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            Search = search,
        }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<StockItem>>(ct) ?? [];
    }

    public async Task<IReadOnlyList<StockWarehouse>> WarehousesAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/items/warehouses", new StockItemSearchRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
        }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<StockWarehouse>>(ct) ?? [];
    }

    public async Task<StockIssueResponse> IssueAsync(StockIssueRequest request, CancellationToken ct)
    {
        request.CustomerId = _tenant.CustomerId ?? Guid.Empty;
        request.OrgId = _tenant.OrgId ?? Guid.Empty;

        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/stock/issue", request, ct);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
        {
            return await response.Content.ReadFromJsonAsync<StockIssueResponse>(ct) ?? new StockIssueResponse();
        }

        response.EnsureSuccessStatusCode();
        return new StockIssueResponse();
    }
}

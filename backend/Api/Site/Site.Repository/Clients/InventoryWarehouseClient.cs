using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Site.Repository.Clients;

public sealed class CreateSiteWarehousePayload
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public long SiteId { get; set; }
    public string SiteCode { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
}

public sealed class CreateSiteWarehouseResult
{
    public long WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
}

public sealed class TransferStockPayload
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public long FromWarehouseId { get; set; }
    public long ToWarehouseId { get; set; }
    public long ItemId { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public interface IInventoryWarehouseClient
{
    Task<long?> CreateSiteWarehouseAsync(
        Guid customerId,
        Guid orgId,
        long siteId,
        string siteCode,
        string siteName,
        CancellationToken ct = default);

    Task<bool> TransferStockAsync(
        Guid customerId,
        Guid orgId,
        long fromWarehouseId,
        long toWarehouseId,
        long itemId,
        decimal quantity,
        string? notes = null,
        CancellationToken ct = default);
}

public sealed class HttpInventoryWarehouseClient : IInventoryWarehouseClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpInventoryWarehouseClient> _log;

    public HttpInventoryWarehouseClient(HttpClient http, ILogger<HttpInventoryWarehouseClient> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<long?> CreateSiteWarehouseAsync(
        Guid customerId,
        Guid orgId,
        long siteId,
        string siteCode,
        string siteName,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new CreateSiteWarehousePayload
            {
                CustomerId = customerId,
                OrgId = orgId,
                SiteId = siteId,
                SiteCode = siteCode,
                SiteName = siteName,
            };

            HttpResponseMessage res = await _http.PostAsJsonAsync("internal/warehouses/site", payload, ct);
            if (!res.IsSuccessStatusCode)
            {
                string error = await res.Content.ReadAsStringAsync(ct);
                _log.LogWarning("Failed to create site warehouse in Inventory ({Status}): {Error}", res.StatusCode, error);
                return null;
            }

            var result = await res.Content.ReadFromJsonAsync<CreateSiteWarehouseResult>(cancellationToken: ct);
            return result?.WarehouseId;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not contact Inventory service to create site warehouse for site {SiteId}", siteId);
            return null;
        }
    }

    public async Task<bool> TransferStockAsync(
        Guid customerId,
        Guid orgId,
        long fromWarehouseId,
        long toWarehouseId,
        long itemId,
        decimal quantity,
        string? notes = null,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new TransferStockPayload
            {
                CustomerId = customerId,
                OrgId = orgId,
                FromWarehouseId = fromWarehouseId,
                ToWarehouseId = toWarehouseId,
                ItemId = itemId,
                Quantity = quantity,
                Notes = notes,
            };

            HttpResponseMessage res = await _http.PostAsJsonAsync("internal/stock/transfer", payload, ct);
            if (!res.IsSuccessStatusCode)
            {
                string error = await res.Content.ReadAsStringAsync(ct);
                _log.LogWarning("Inventory stock transfer refused ({Status}): {Error}", res.StatusCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not contact Inventory service for stock transfer");
            return false;
        }
    }
}


using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Tenancy;

namespace Sales.Repository.Clients;

/// <summary>The branch's UPI id, for the payment QR on an unpaid invoice (TK-158).</summary>
public interface IUpiIdProvider
{
    /// <summary>The id, or null when the branch has none or Master cannot be reached.</summary>
    Task<string?> GetUpiIdAsync(CancellationToken ct = default);
}

/// <summary>
/// Reads the UPI id from Master's org context. Cached for a minute rather than
/// the six hours the seller block gets: a branch that changes its UPI id must
/// not go on printing QR codes that send customers' money to the old one.
/// Failing to read it prints no QR, never a guessed one.
/// </summary>
public sealed class HttpUpiIdProvider : IUpiIdProvider
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpUpiIdProvider> _log;

    public HttpUpiIdProvider(HttpClient http, IMemoryCache cache, ITenantContext tenant, ILogger<HttpUpiIdProvider> log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    public async Task<string?> GetUpiIdAsync(CancellationToken ct = default)
    {
        if (_tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        string key = $"upi-id:{orgId}";
        if (_cache.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        try
        {
            UpiContext? context = await _http.GetFromJsonAsync<UpiContext>($"internal/orgs/{orgId}/context", ct);
            string? upiId = string.IsNullOrWhiteSpace(context?.UpiId) ? null : context.UpiId.Trim();
            _cache.Set(key, upiId, TimeSpan.FromMinutes(1));
            return upiId;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not read the UPI id of organization {OrgId}; the invoice prints without a payment QR", orgId);
            return null;
        }
    }

    private sealed record UpiContext(string? UpiId);
}

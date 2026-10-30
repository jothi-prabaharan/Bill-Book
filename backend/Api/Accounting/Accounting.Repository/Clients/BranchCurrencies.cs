using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Tenancy;

namespace Accounting.Repository.Clients;

/// <summary>
/// The currencies a branch may use, by ISO code: its base currency and each other one it has switched
/// on in Master. An account is refused a currency that is not on this list, so a typo or an unused code
/// cannot become a currency the books are then kept in. Null when the list cannot be read, so the caller
/// refuses rather than guessing.
/// </summary>
public interface IBranchCurrencies
{
    Task<IReadOnlyList<string>?> ActiveCodesAsync(CancellationToken ct = default);
}

/// <summary>
/// Reads the list from Master's internal organization context, cached per branch. It changes rarely, and
/// the alternative is an HTTP call on every account save.
/// </summary>
public sealed class HttpBranchCurrencies : IBranchCurrencies
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpBranchCurrencies> _log;

    public HttpBranchCurrencies(
        HttpClient http,
        IMemoryCache cache,
        ITenantContext tenant,
        ILogger<HttpBranchCurrencies> log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    public async Task<IReadOnlyList<string>?> ActiveCodesAsync(CancellationToken ct = default)
    {
        if (_tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        string key = $"active-currencies:{orgId}";

        if (_cache.TryGetValue(key, out IReadOnlyList<string>? cached))
        {
            return cached;
        }

        try
        {
            OrgContextDto? context = await _http.GetFromJsonAsync<OrgContextDto>(
                $"internal/orgs/{orgId}/context", ct);

            if (context?.ActiveCurrencyCodes is not { } codes)
            {
                _log.LogWarning("Organization {OrgId} reported no active currency list", orgId);
                return null;
            }

            IReadOnlyList<string> list = codes.Select(c => c.ToUpperInvariant()).ToList();
            _cache.Set(key, list, TimeSpan.FromMinutes(5));
            return list;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not read the active currencies for organization {OrgId}", orgId);
            return null;
        }
    }

    private sealed record OrgContextDto(IReadOnlyList<string>? ActiveCurrencyCodes);
}

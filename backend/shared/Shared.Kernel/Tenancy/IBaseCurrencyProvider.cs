using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// The currency the branch keeps its books in.
///
/// Every general-ledger row is denominated in it, and the base-currency columns
/// are what every report sums. It lives in <c>mst.Organizations</c>, in the
/// master database, which no per-customer service may read.
/// </summary>
public interface IBaseCurrencyProvider
{
    /// <summary>
    /// The current branch's base currency, or <c>null</c> when it could not be
    /// established. Null rather than a default on purpose — see the
    /// implementation.
    /// </summary>
    Task<string?> GetBaseCurrencyAsync(CancellationToken ct = default);
}

/// <summary>
/// Reads it from Platform's org context, cached per organization for the same
/// reason <see cref="Shared.Kernel.Numbering.HttpFinancialYearProvider"/> caches
/// the financial year: it changes about never, and the alternative is an HTTP
/// call on every posting.
///
/// <b>It answers null rather than guessing.</b> That is the one place this
/// differs from the financial-year provider, and deliberately: a number composed
/// under the wrong year segment is wrong on a document somebody reads, while a
/// ledger row stamped with the wrong currency is wrong in a total nobody
/// re-reads. Every caller of this posts to the ledger, and every one of them can
/// be retried, so a brief outage should postpone a posting rather than book it
/// in a currency that was assumed.
/// </summary>
public sealed class HttpBaseCurrencyProvider : IBaseCurrencyProvider
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpBaseCurrencyProvider> _log;

    public HttpBaseCurrencyProvider(
        HttpClient http,
        IMemoryCache cache,
        ITenantContext tenant,
        ILogger<HttpBaseCurrencyProvider> log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    public async Task<string?> GetBaseCurrencyAsync(CancellationToken ct = default)
    {
        if (_tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        string key = $"base-currency:{orgId}";

        if (_cache.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        try
        {
            OrgContextDto? context = await _http.GetFromJsonAsync<OrgContextDto>(
                $"internal/orgs/{orgId}/context", ct);

            // A three-letter code or nothing. Anything else is a Platform that
            // has changed shape, and stamping it onto ledger rows would spread
            // the problem across the general ledger before anyone noticed.
            if (context?.BaseCurrency is not { Length: 3 } currency)
            {
                _log.LogWarning(
                    "Organization {OrgId} reported base currency {Currency}, which is not a "
                        + "three-letter code",
                    orgId,
                    context?.BaseCurrency);

                return null;
            }

            _cache.Set(key, currency, TimeSpan.FromHours(6));
            return currency;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not read the base currency for organization {OrgId}", orgId);
            return null;
        }
    }

    /// <summary>Only the one field is read; the rest of the context belongs to its own callers.</summary>
    private sealed record OrgContextDto(string? BaseCurrency);
}

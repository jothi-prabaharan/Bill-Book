using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Tax;

/// <summary>
/// The GST rates in force on a date, read from Accounting.
///
/// <b>Keyed by date, not by "now".</b> A document is taxed at the rate in force
/// when it was raised, so an invoice reopened after a rate revision must not
/// reprice itself. Every method here takes the document's date and none of them
/// consults a clock.
/// </summary>
public interface ITaxRateProvider
{
    /// <summary>
    /// Every rate in force on <paramref name="onDate"/>, by tax group. Null when
    /// Accounting could not be reached — null rather than empty, because "no
    /// rates" and "could not ask" lead to opposite decisions.
    /// </summary>
    Task<IReadOnlyDictionary<long, TaxRate>?> GetRatesAsync(
        DateOnly onDate, CancellationToken ct = default);

    /// <summary>One group's rate, or null if it has none in force on that date.</summary>
    Task<TaxRate?> GetRateAsync(long taxGroupId, DateOnly onDate, CancellationToken ct = default);
}

/// <summary>
/// Reads them from Accounting's internal API, cached per organization and date.
///
/// <b>The cache key includes the date</b>, which is what makes it safe: an
/// effective-dated table cached under "the current rates" is a backdated
/// document taxed at today's figures, and nothing about the result would look
/// wrong.
///
/// <b>It answers null rather than guessing</b>, the same way
/// <see cref="IBaseCurrencyProvider"/> does and for the same reason: every
/// caller of this is about to write a document, and a brief outage should
/// postpone that rather than commit a figure that was assumed. A wrong rate
/// prints, posts and balances.
/// </summary>
public sealed class HttpTaxRateProvider : ITaxRateProvider
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpTaxRateProvider> _log;

    public HttpTaxRateProvider(
        HttpClient http,
        IMemoryCache cache,
        ITenantContext tenant,
        ILogger<HttpTaxRateProvider> log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    public async Task<IReadOnlyDictionary<long, TaxRate>?> GetRatesAsync(
        DateOnly onDate, CancellationToken ct = default)
    {
        if (_tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        string key = $"tax-rates:{orgId}:{onDate:yyyy-MM-dd}";

        if (_cache.TryGetValue(key, out IReadOnlyDictionary<long, TaxRate>? cached))
        {
            return cached;
        }

        try
        {
            List<TaxRate>? rates = await _http.GetFromJsonAsync<List<TaxRate>>(
                $"internal/tax/rates?on={onDate:yyyy-MM-dd}", ct);

            if (rates is null)
            {
                return null;
            }

            // Last one wins if a group somehow has two in force — which the
            // effective-dating is meant to prevent, so it is logged rather than
            // silently resolved.
            Dictionary<long, TaxRate> byGroup = [];
            foreach (TaxRate rate in rates)
            {
                if (!byGroup.TryAdd(rate.TaxGroupId, rate))
                {
                    _log.LogWarning(
                        "Tax group {TaxGroupId} has more than one rate in force on {OnDate}",
                        rate.TaxGroupId,
                        onDate);
                }
            }

            // Short, and deliberately shorter than the base currency's six hours.
            // A rate revision is published against a future date and takes effect
            // on it; the financial year and the base currency change about never.
            _cache.Set(key, (IReadOnlyDictionary<long, TaxRate>)byGroup, TimeSpan.FromMinutes(10));
            return byGroup;
        }
        catch (Exception ex)
        {
            _log.LogWarning(
                ex, "Could not read tax rates for organization {OrgId} on {OnDate}", orgId, onDate);
            return null;
        }
    }

    public async Task<TaxRate?> GetRateAsync(
        long taxGroupId, DateOnly onDate, CancellationToken ct = default)
    {
        IReadOnlyDictionary<long, TaxRate>? rates = await GetRatesAsync(onDate, ct);
        return rates is not null && rates.TryGetValue(taxGroupId, out TaxRate? rate) ? rate : null;
    }
}

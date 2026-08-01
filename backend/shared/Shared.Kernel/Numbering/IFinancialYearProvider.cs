using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Numbering;

/// <summary>
/// The branch's financial year start month — 4 for India, and genuinely
/// different elsewhere.
///
/// It decides the year segment in every generated number and the boundary a
/// yearly counter resets on, so a wrong value does not fail: it silently issues
/// <c>INV/2526/…</c> when the branch is on a calendar year and should be issuing
/// <c>INV/2025/…</c>. That is the kind of error nobody notices until an auditor
/// asks why the series restarts in April.
/// </summary>
public interface IFinancialYearProvider
{
    /// <summary>The current branch's start month. Never throws — see the implementation.</summary>
    Task<int> GetStartMonthAsync(CancellationToken ct = default);
}

/// <summary>
/// Reads it from Platform's org context, cached per organization.
///
/// The value lives in <c>plt.Organizations</c>, in the master database, which no
/// service may read directly. It changes about never, so it is cached for hours
/// rather than fetched per number — the alternative is an HTTP call on every
/// save that allocates a code.
/// </summary>
public sealed class HttpFinancialYearProvider : IFinancialYearProvider
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly NumberingOptions _options;
    private readonly ILogger<HttpFinancialYearProvider> _log;

    public HttpFinancialYearProvider(
        HttpClient http,
        IMemoryCache cache,
        ITenantContext tenant,
        IOptions<NumberingOptions> options,
        ILogger<HttpFinancialYearProvider> log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _options = options.Value;
        _log = log;
    }

    public async Task<int> GetStartMonthAsync(CancellationToken ct = default)
    {
        if (_tenant.OrgId is not Guid orgId)
        {
            // Seeding and design-time paths have no tenant. The configured value
            // is the right answer there, and there is no branch to ask about.
            return _options.FinancialYearStartMonth;
        }

        string key = $"fy-start-month:{orgId}";

        if (_cache.TryGetValue(key, out int cached))
        {
            return cached;
        }

        try
        {
            OrgContextDto? context = await _http.GetFromJsonAsync<OrgContextDto>(
                $"internal/orgs/{orgId}/context", ct);

            int month = context?.FinancialYearStartMonth ?? 0;

            if (month is < 1 or > 12)
            {
                // A value outside 1–12 would produce a nonsense year segment.
                // Fall back rather than compose a number nobody can explain.
                _log.LogWarning(
                    "Organization {OrgId} reported financial year start month {Month}; "
                        + "using the configured default instead",
                    orgId,
                    month);

                return _options.FinancialYearStartMonth;
            }

            _cache.Set(key, month, TimeSpan.FromHours(6));
            return month;
        }
        catch (Exception ex)
        {
            // Never throws. Platform being briefly unreachable must not stop a
            // contact or an item being saved — the configured default is right
            // for the overwhelming majority of branches, and the alternative is
            // refusing to allocate a code at all.
            _log.LogWarning(
                ex,
                "Could not read the financial year start month for {OrgId}; "
                    + "using the configured default",
                orgId);

            return _options.FinancialYearStartMonth;
        }
    }

    /// <summary>Only the one field is read; the rest of the context is Identity's business.</summary>
    private sealed record OrgContextDto(int FinancialYearStartMonth);
}

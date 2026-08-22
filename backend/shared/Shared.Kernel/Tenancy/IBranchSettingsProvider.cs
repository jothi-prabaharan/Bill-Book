using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Settings for the branch that affect document computation.
/// </summary>
public interface IBranchSettingsProvider
{
    /// <summary>
    /// The current branch's state code and discount preference, or <c>null</c> when it could not be
    /// established.
    /// </summary>
    Task<BranchSettings?> GetSettingsAsync(CancellationToken ct = default);
}

public sealed record BranchSettings(
    string StateCode,
    bool DiscountBeforeTax,

    /// <summary>
    /// Whether this branch trades from a Union Territory without a legislature,
    /// so its intra-state supplies carry UTGST in place of SGST. Defaulted false
    /// because almost every branch is in a state.
    /// </summary>
    bool IsUnionTerritory = false);

/// <summary>
/// Reads the settings from Platform's org context, cached per organization.
/// </summary>
public sealed class HttpBranchSettingsProvider : IBranchSettingsProvider
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpBranchSettingsProvider> _log;

    public HttpBranchSettingsProvider(
        HttpClient http,
        IMemoryCache cache,
        ITenantContext tenant,
        ILogger<HttpBranchSettingsProvider> log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    public async Task<BranchSettings?> GetSettingsAsync(CancellationToken ct = default)
    {
        if (_tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        string key = $"branch-settings:{orgId}";

        if (_cache.TryGetValue(key, out BranchSettings? cached))
        {
            return cached;
        }

        try
        {
            OrgContextDto? context = await _http.GetFromJsonAsync<OrgContextDto>(
                $"internal/orgs/{orgId}/context", ct);

            // A two-digit state code or nothing.
            if (context?.StateCode is not { Length: 2 } stateCode)
            {
                _log.LogWarning(
                    "Organization {OrgId} reported state code {StateCode}, which is not a "
                        + "two-digit code",
                    orgId,
                    context?.StateCode);

                return null;
            }

            BranchSettings settings = new(stateCode, context.DiscountBeforeTax);
            _cache.Set(key, settings, TimeSpan.FromHours(6));
            return settings;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not read the settings for organization {OrgId}", orgId);
            return null;
        }
    }

    /// <summary>Only the required fields are read.</summary>
    private sealed record OrgContextDto(string? StateCode, bool DiscountBeforeTax);
}

using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Who the branch is, for the seller block on a document it issues.
///
/// <b>This exists because an invoice was printing "Our Company" and a GSTIN
/// belonging to nobody.</b> The two were hard-coded in <c>InvoiceService</c>
/// under a TODO, and every customer's invoice — the tax document their buyer
/// files an input credit against — carried the same made-up registration.
///
/// It is a provider rather than a query because <c>mst.Organizations</c> is the
/// master database and Sales cannot read it (CLAUDE.md 8). Master already serves
/// this on <c>internal/orgs/{orgId}/context</c> for the state code and the
/// financial year; the name, GSTIN and address ride along on the same call and
/// the same six-hour cache, so printing an invoice costs no extra round trip.
///
/// <b>Null is not a licence to invent one.</b> When the branch cannot be
/// resolved the caller must refuse to issue the document rather than print a
/// placeholder — a tax invoice with the wrong seller on it is worse than no
/// invoice, because it looks filed.
/// </summary>
public interface IOrgIdentityProvider
{
    Task<OrgIdentity?> GetIdentityAsync(CancellationToken ct = default);
}

/// <summary>
/// The branch as it appears at the top of a document it issued.
///
/// <see cref="Gstin"/> is null on an unregistered branch, which prints as absent
/// rather than as a placeholder.
/// </summary>
public sealed record OrgIdentity(
    string Name,
    string? Gstin,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateCode,
    string? PostalCode);

/// <summary>
/// Reads the identity from Master's org context, cached per organization
/// alongside <see cref="HttpBranchSettingsProvider"/> and for the same six
/// hours: a branch's registered name and GSTIN change about never, and a
/// document that took an HTTP call to name its own seller would make posting an
/// invoice depend on Master being up.
/// </summary>
public sealed class HttpOrgIdentityProvider : IOrgIdentityProvider
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpOrgIdentityProvider> _log;

    public HttpOrgIdentityProvider(
        HttpClient http,
        IMemoryCache cache,
        ITenantContext tenant,
        ILogger<HttpOrgIdentityProvider> log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    public async Task<OrgIdentity?> GetIdentityAsync(CancellationToken ct = default)
    {
        if (_tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        string key = $"org-identity:{orgId}";

        if (_cache.TryGetValue(key, out OrgIdentity? cached))
        {
            return cached;
        }

        try
        {
            OrgContextDto? context = await _http.GetFromJsonAsync<OrgContextDto>(
                $"internal/orgs/{orgId}/context", ct);

            if (context?.OrgName is not { Length: > 0 } name)
            {
                _log.LogWarning(
                    "Organization {OrgId} reported no name, so no seller block can be "
                        + "composed for its documents",
                    orgId);

                return null;
            }

            var identity = new OrgIdentity(
                name,
                context.Gstin,
                context.AddressLine1,
                context.AddressLine2,
                context.City,
                context.StateCode,
                context.PostalCode);

            _cache.Set(key, identity, TimeSpan.FromHours(6));

            return identity;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not read the identity of organization {OrgId}", orgId);

            return null;
        }
    }

    /// <summary>Only the fields a seller block needs are read.</summary>
    private sealed record OrgContextDto(
        string? OrgName,
        string? Gstin,
        string? AddressLine1,
        string? AddressLine2,
        string? City,
        string? StateCode,
        string? PostalCode);
}

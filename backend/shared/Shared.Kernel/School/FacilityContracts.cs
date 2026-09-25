using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.School;

// What the maintenance services ask Facility (S6–S8, TK-66 onward): whether an
// asset or space exists in the branch, and what it is called. Ids across
// services are unenforced (hard rule 8), so they are checked here.

public sealed class FacilityLookupRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public List<long> AssetIds { get; set; } = [];

    public List<long> SpaceIds { get; set; } = [];
}

public sealed class FacilityLookupResponse
{
    public List<FacilityItem> Assets { get; set; } = [];

    public List<FacilityItem> Spaces { get; set; } = [];
}

public sealed class FacilityItem
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>An active space, or an asset that is not disposed.</summary>
    public bool IsUsable { get; set; }
}

public interface IFacilityClient
{
    /// <summary>Throws when Facility cannot be asked.</summary>
    Task<FacilityLookupResponse> LookupAsync(IEnumerable<long> assetIds, IEnumerable<long> spaceIds, CancellationToken ct);
}

public sealed class HttpFacilityClient : IFacilityClient
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpFacilityClient(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<FacilityLookupResponse> LookupAsync(IEnumerable<long> assetIds, IEnumerable<long> spaceIds, CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/facility/lookup", new FacilityLookupRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            AssetIds = [.. assetIds.Distinct()],
            SpaceIds = [.. spaceIds.Distinct()],
        }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FacilityLookupResponse>(ct) ?? new FacilityLookupResponse();
    }
}

using System.Net.Http.Json;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Stock;

/// <summary>A unit of measure's GST unit (UQC), e.g. <c>NOS</c>, <c>KGS</c>, <c>OTH</c> (TK-91).</summary>
public sealed record UqcRef(long UomId, string UqcCode);

public interface IUqcLookup
{
    /// <summary>
    /// The UQC of each unit among <paramref name="uomIds"/> in the current branch.
    /// Throws when Inventory cannot be asked; a unit it does not know is absent.
    /// </summary>
    Task<IReadOnlyDictionary<long, string>> FindAsync(IEnumerable<long> uomIds, CancellationToken ct);
}

/// <summary>Inventory's <c>internal/items/uqc</c>, under the internal key, with the branch in the body.</summary>
public sealed class HttpUqcLookup : IUqcLookup
{
    /// <summary>What a line with no unit, or with a unit Inventory no longer knows, reports as.</summary>
    public const string Other = "OTH";

    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpUqcLookup(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<IReadOnlyDictionary<long, string>> FindAsync(IEnumerable<long> uomIds, CancellationToken ct)
    {
        List<long> wanted = [.. uomIds.Distinct()];
        if (wanted.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/items/uqc", new NameLookupRequest
        {
            Ids = wanted,
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
        }, ct);
        response.EnsureSuccessStatusCode();

        List<UqcRef> found = await response.Content.ReadFromJsonAsync<List<UqcRef>>(ct) ?? [];
        return found.ToDictionary(u => u.UomId, u => u.UqcCode);
    }
}

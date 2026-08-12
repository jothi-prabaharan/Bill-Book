using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Documents;

/// <summary>A code and a name for something a document refers to by id.</summary>
public sealed record NamedRef(long Id, string Code, string Name);

/// <summary>The ids to resolve. A body rather than a query string — see the implementations.</summary>
public sealed class NameLookupRequest
{
    public List<long> Ids { get; set; } = [];
}

/// <summary>
/// Resolves the names a document deliberately does not store.
///
/// <b>This exists because of a decision, not an oversight.</b> `SALES.md` keeps
/// `ContactName`, `ItemCode` and `ItemName` off the document so that correcting a
/// misspelling shows everywhere, including on documents already raised. The cost
/// it names is a cross-service lookup on every list screen — and the cost only
/// stays acceptable if it is <b>batched</b>, which is what this interface is for.
///
/// <b>The batching is the whole point.</b> A quote list of fifty rows resolving
/// one name at a time is fifty HTTP round trips to Contacts, and it will be the
/// slowest screen in the product. One call for fifty ids is one round trip. The
/// interface therefore takes a collection and has no single-id overload, so the
/// N+1 cannot be written by accident.
/// </summary>
public interface IContactNameLookup
{
    Task<IReadOnlyDictionary<long, NamedRef>> ResolveAsync(
        IReadOnlyCollection<long> ids, CancellationToken ct = default);
}

/// <summary>The same, for items. Separate interfaces because they are separate services.</summary>
public interface IItemNameLookup
{
    Task<IReadOnlyDictionary<long, NamedRef>> ResolveAsync(
        IReadOnlyCollection<long> ids, CancellationToken ct = default);
}

/// <summary>
/// Shared plumbing for both lookups: batch, cache per organization and id, and
/// never fail the caller.
///
/// <b>A name that cannot be read is not an error.</b> Every caller is rendering a
/// list, and a contact service having a bad minute should leave one column
/// showing an id rather than turning a whole screen into a 500. That is the
/// opposite of <see cref="Shared.Kernel.Tax.ITaxRateProvider"/>, which answers
/// null and stops the save — and deliberately so: a rate is written into the
/// books, a name is only drawn on a screen.
/// </summary>
public abstract class HttpNameLookup
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger _log;

    protected HttpNameLookup(
        HttpClient http, IMemoryCache cache, ITenantContext tenant, ILogger log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    /// <summary>The internal route, relative to the service's base address.</summary>
    protected abstract string Route { get; }

    /// <summary>What the cache key is prefixed with, so contacts and items cannot collide.</summary>
    protected abstract string CachePrefix { get; }

    public async Task<IReadOnlyDictionary<long, NamedRef>> ResolveAsync(
        IReadOnlyCollection<long> ids, CancellationToken ct = default)
    {
        Dictionary<long, NamedRef> resolved = [];

        if (_tenant.OrgId is not Guid orgId || ids.Count == 0)
        {
            return resolved;
        }

        // Only the ones not already cached go over the wire. A list screen paged
        // through twice asks for almost nothing the second time.
        List<long> missing = [];

        foreach (long id in ids.Distinct())
        {
            if (_cache.TryGetValue($"{CachePrefix}:{orgId}:{id}", out NamedRef? hit) && hit is not null)
            {
                resolved[id] = hit;
            }
            else
            {
                missing.Add(id);
            }
        }

        if (missing.Count == 0)
        {
            return resolved;
        }

        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync(
                Route, new NameLookupRequest { Ids = missing }, ct);

            response.EnsureSuccessStatusCode();

            List<NamedRef>? names = await response.Content
                .ReadFromJsonAsync<List<NamedRef>>(ct);

            foreach (NamedRef name in names ?? [])
            {
                resolved[name.Id] = name;

                // Names change rarely and a stale one for a few minutes is a
                // cosmetic fault, which is the trade this cache is making.
                _cache.Set($"{CachePrefix}:{orgId}:{name.Id}", name, TimeSpan.FromMinutes(5));
            }
        }
        catch (Exception ex)
        {
            // Logged at warning, not error, and swallowed: the caller renders a
            // list and will show ids for whatever is missing.
            _log.LogWarning(
                ex,
                "Could not resolve {Count} name(s) via {Route} for organization {OrgId}",
                missing.Count,
                Route,
                orgId);
        }

        return resolved;
    }
}

/// <summary>Contact names, from Contacts.</summary>
public sealed class HttpContactNameLookup : HttpNameLookup, IContactNameLookup
{
    public HttpContactNameLookup(
        HttpClient http,
        IMemoryCache cache,
        ITenantContext tenant,
        ILogger<HttpContactNameLookup> log)
        : base(http, cache, tenant, log)
    {
    }

    protected override string Route => "internal/contacts/names";

    protected override string CachePrefix => "contact-name";
}

/// <summary>Item names and codes, from Inventory.</summary>
public sealed class HttpItemNameLookup : HttpNameLookup, IItemNameLookup
{
    public HttpItemNameLookup(
        HttpClient http,
        IMemoryCache cache,
        ITenantContext tenant,
        ILogger<HttpItemNameLookup> log)
        : base(http, cache, tenant, log)
    {
    }

    protected override string Route => "internal/items/names";

    protected override string CachePrefix => "item-name";
}

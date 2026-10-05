using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Documents;

/// <summary>A user as another service shows one: a waiter on an order (TK-146).</summary>
public sealed record UserRef(Guid Id, string Name);

/// <summary>The user ids to resolve, and the branch whose staff they must be.</summary>
public sealed class UserNameLookupRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public List<Guid> Ids { get; set; } = [];
}

/// <summary>
/// User names from Master, batched (rule 8: users live in the master database,
/// so a user id on a tenant row is a plain Guid resolved in C#). Only users
/// with an active role in the branch come back, so an id that does not resolve
/// is not one of the branch's staff.
/// </summary>
public interface IUserNameLookup
{
    Task<IReadOnlyDictionary<Guid, UserRef>> ResolveAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}

/// <summary>
/// Master's <c>internal/users/names</c>, cached per branch and user for five
/// minutes. Like the other name lookups it never fails its caller: a name that
/// cannot be read is left out and logged.
/// </summary>
public sealed class HttpUserNameLookup : IUserNameLookup
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpUserNameLookup> _log;

    public HttpUserNameLookup(HttpClient http, IMemoryCache cache, ITenantContext tenant, ILogger<HttpUserNameLookup> log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    public async Task<IReadOnlyDictionary<Guid, UserRef>> ResolveAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        Dictionary<Guid, UserRef> resolved = [];
        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId || ids.Count == 0)
        {
            return resolved;
        }

        List<Guid> missing = [];
        foreach (Guid id in ids.Distinct())
        {
            if (_cache.TryGetValue($"user-name:{orgId}:{id}", out UserRef? hit) && hit is not null)
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
                "internal/users/names", new UserNameLookupRequest { CustomerId = customerId, OrgId = orgId, Ids = missing }, ct);
            response.EnsureSuccessStatusCode();

            foreach (UserRef user in await response.Content.ReadFromJsonAsync<List<UserRef>>(ct) ?? [])
            {
                resolved[user.Id] = user;
                _cache.Set($"user-name:{orgId}:{user.Id}", user, TimeSpan.FromMinutes(5));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Could not resolve {Count} user name(s) for organization {OrgId}", missing.Count, orgId);
        }

        return resolved;
    }
}

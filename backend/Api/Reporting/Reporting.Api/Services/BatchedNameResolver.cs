using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Reporting.Api.Services;

/// <summary>
/// Resolves cross-database names from Master in batches to avoid N+1 lookups
/// during report generation.
/// </summary>
public sealed class BatchedNameResolver
{
    private readonly HttpClient _client;
    private readonly IMemoryCache _cache;

    public BatchedNameResolver(HttpClient client, IMemoryCache cache)
    {
        _client = client;
        _cache = cache;
    }

    /// <summary>
    /// Gets display names for the given user ids. Missing or null ids are skipped.
    /// </summary>
    public async Task<Dictionary<Guid, string>> GetUserNamesAsync(IEnumerable<Guid?> userIds, CancellationToken ct = default)
    {
        var distinctIds = userIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var response = await _client.PostAsJsonAsync("internal/users/batch", distinctIds, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Dictionary<Guid, string>>(cancellationToken: ct) 
            ?? new Dictionary<Guid, string>();
    }

    /// <summary>
    /// Gets the display name for every account type, cached since they are fixed.
    /// </summary>
    public async Task<Dictionary<int, string>> GetAccountTypeNamesAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync("AccountTypeNames", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var response = await _client.GetAsync("internal/master/account-types", ct);
            response.EnsureSuccessStatusCode();

            var types = await response.Content.ReadFromJsonAsync<List<AccountTypeDto>>(cancellationToken: ct);
            return types?.ToDictionary(t => t.AccountTypeId, t => t.DisplayName) ?? new Dictionary<int, string>();
        }) ?? new Dictionary<int, string>();
    }

    private sealed record AccountTypeDto(int AccountTypeId, string SystemName, string DisplayName);
}

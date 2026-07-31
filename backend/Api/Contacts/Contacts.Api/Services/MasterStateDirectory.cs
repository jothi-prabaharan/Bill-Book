using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Contacts.Api.Services;

/// <summary>
/// States, read from Master. Contacts holds an unenforced StateId — a
/// cross-database FK is impossible — and needs the state's GST code to check a
/// GSTIN's first two digits.
/// </summary>
public interface IStateDirectory
{
    /// <summary>The state's GST code, or null when the id is unknown.</summary>
    Task<string?> GetStateCodeAsync(int stateId, CancellationToken ct);
}

public sealed class MasterStateDirectory : IStateDirectory
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public MasterStateDirectory(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<string?> GetStateCodeAsync(int stateId, CancellationToken ct)
    {
        // State codes are legislated and effectively immutable, so a long cache
        // costs nothing and keeps contact saves off the network.
        if (_cache.TryGetValue($"state:{stateId}", out string? cached))
        {
            return cached;
        }

        HttpResponseMessage response = await _http.GetAsync($"api/master/states/{stateId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        StateResponse? state = await response.Content.ReadFromJsonAsync<StateResponse>(ct);
        if (state is null)
        {
            return null;
        }

        _cache.Set($"state:{stateId}", state.StateCode, TimeSpan.FromHours(12));
        return state.StateCode;
    }

    private sealed record StateResponse(int StateId, int CountryId, string StateCode, string StateName, bool IsActive);
}

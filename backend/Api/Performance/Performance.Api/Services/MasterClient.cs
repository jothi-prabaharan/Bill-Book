using System.Net.Http.Json;
using Shared.Kernel.Approvals;

namespace Performance.Api.Services;

public interface IMasterClient
{
    Task<ResolveChainResponse?> ResolveChainAsync(ResolveChainRequest req, CancellationToken ct);
}

public sealed class MasterClient : IMasterClient
{
    private readonly HttpClient _http;

    public MasterClient(HttpClient http) => _http = http;

    public async Task<ResolveChainResponse?> ResolveChainAsync(ResolveChainRequest req, CancellationToken ct)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("internal/approval-chains/resolve", req, ct);
            if (!res.IsSuccessStatusCode) return null;
            return await res.Content.ReadFromJsonAsync<ResolveChainResponse>(cancellationToken: ct);
        }
        catch
        {
            return null;
        }
    }
}

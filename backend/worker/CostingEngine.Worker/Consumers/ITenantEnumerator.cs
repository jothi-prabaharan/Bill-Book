using System.Net.Http.Json;

namespace CostingEngine.Worker.Consumers;

/// <summary>One branch to process, and the database it lives in.</summary>
public sealed record ActiveOrganization(Guid CustomerId, Guid OrgId, string DatabaseName);

/// <summary>
/// The branches this worker should walk. A background worker has no request to
/// take a tenant from, so it asks Platform for the list — the same directory the
/// request path uses, rather than a second copy that could disagree with it.
/// </summary>
public interface ITenantEnumerator
{
    Task<IReadOnlyList<ActiveOrganization>> ListAsync(CancellationToken ct);
}

public sealed class HttpTenantEnumerator : ITenantEnumerator
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpTenantEnumerator> _log;

    public HttpTenantEnumerator(HttpClient http, ILogger<HttpTenantEnumerator> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<IReadOnlyList<ActiveOrganization>> ListAsync(CancellationToken ct)
    {
        try
        {
            List<ActiveOrganization>? rows = await _http
                .GetFromJsonAsync<List<ActiveOrganization>>(
                    "internal/customers/active-organizations", ct);

            return rows ?? [];
        }
        catch (Exception ex)
        {
            // Platform being briefly unreachable is not a costing failure. The
            // next tick asks again; nothing is lost, because the queue is a
            // column in the database rather than a message that expires.
            _log.LogWarning(ex, "Could not read the list of active organizations");
            return [];
        }
    }
}

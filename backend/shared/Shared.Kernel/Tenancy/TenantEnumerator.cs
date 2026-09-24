using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Tenancy;

/// <summary>One branch to process.</summary>
public sealed record ActiveOrganization(Guid CustomerId, Guid OrgId);

/// <summary>
/// The branches a background worker should walk. A worker has no request to
/// take a tenant from, so it asks Master for the list — the same directory the
/// request path uses, rather than a second copy that could disagree with it.
///
/// In Shared.Kernel since TK-20, when the payment reminders became the second
/// worker to need it; it was the costing engine's own before.
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
            // Master being briefly unreachable is not a failure of the work. The
            // next tick asks again; nothing is lost, because the work is in the
            // database rather than in a message that expires.
            _log.LogWarning(ex, "Could not read the list of active organizations");
            return [];
        }
    }
}

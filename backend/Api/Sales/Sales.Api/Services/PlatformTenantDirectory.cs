using System.Net;
using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services;

/// <summary>
/// The tenant directory, read from Platform's internal API. No service reads the
/// plt schema directly, and the response carries only the Key Vault reference —
/// the connection string itself is resolved locally from the secret store, so
/// the credential never travels over HTTP.
/// </summary>
public sealed class PlatformTenantDirectory : ITenantDirectory
{
    private readonly HttpClient _http;

    public PlatformTenantDirectory(HttpClient http) => _http = http;

    public async Task<TenantDatabase?> LookupAsync(Guid customerId, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.GetAsync(
            $"internal/customers/{customerId}/database", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantDatabase>(ct);
    }
}

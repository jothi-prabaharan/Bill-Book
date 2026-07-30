using System.Net;
using System.Net.Http.Json;

namespace Identity.Api.Services;

/// <summary>
/// Calls the Platform service to resolve an org's customer and licence.
/// The Platform side must expose GET /internal/orgs/{orgId}/context returning
/// the <see cref="OrgContext"/> shape. Configure the base address via the
/// "Platform" named HttpClient.
/// </summary>
public sealed class PlatformDirectory : IPlatformDirectory
{
    private readonly HttpClient _http;

    public PlatformDirectory(HttpClient http) => _http = http;

    public async Task<OrgContext?> ResolveOrgAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _http.GetAsync($"internal/orgs/{orgId}/context", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrgContext>(cancellationToken);
    }
}

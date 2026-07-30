using System.Net.Http.Json;

namespace Platform.Api.Services;

/// <summary>
/// Cross-service seam to Identity: creates the owner user during provisioning.
/// Platform must never touch the idn tables directly, so this goes through
/// Identity's internal API. Internal-only — must not be routed via the gateway.
/// </summary>
public interface IIdentityAdmin
{
    Task CreateOwnerUserAsync(CreateOwnerUser request, CancellationToken ct = default);
}

public sealed record CreateOwnerUser(
    Guid OrgId,
    string Email,
    string DisplayName,
    string? MobileNumber,
    string Password);

public sealed class IdentityAdminClient : IIdentityAdmin
{
    private readonly HttpClient _http;

    public IdentityAdminClient(HttpClient http) => _http = http;

    public async Task CreateOwnerUserAsync(CreateOwnerUser request, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync("internal/users/owner", request, ct);
        response.EnsureSuccessStatusCode();
    }
}

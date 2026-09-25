namespace Employee.Api.Services;

public interface IMasterUserClient
{
    Task DeactivateUserAsync(Guid userId, CancellationToken ct);
}

public sealed class MasterUserClient : IMasterUserClient
{
    private readonly HttpClient _http;

    public MasterUserClient(HttpClient http) => _http = http;

    public async Task DeactivateUserAsync(Guid userId, CancellationToken ct)
    {
        var resp = await _http.PostAsync($"internal/users/{userId}/deactivate", null, ct);
        resp.EnsureSuccessStatusCode();
    }
}

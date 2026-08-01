using System.Net.Http.Json;
using Shared.Kernel.Internal;

namespace Platform.Api.Services;

/// <summary>
/// Writes every service's master data for a newly created organization.
///
/// This replaces a fan-out that never happened. Provisioning published a
/// <c>CustomerProvisioned</c> event through <c>IEventPublisher</c>, whose only
/// implementation logs "EVENT (not delivered)" — so eight seed methods sat with
/// no caller and a new organization came up with no chart of accounts, no tax
/// rates, no units and no numbering series. An item could not be saved at all,
/// because an item needs a unit type.
///
/// Calls rather than events, matching how every other cross-service hop in this
/// codebase already works. The event is still published, so swapping in a real
/// bus later is a matter of deleting this and implementing consumers.
/// </summary>
public interface ITenantSeeder
{
    /// <summary>Seeds every service. Returns the services that could not be reached.</summary>
    Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct);
}

public sealed class HttpTenantSeeder : ITenantSeeder
{
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _config;
    private readonly ILogger<HttpTenantSeeder> _log;

    /// <summary>
    /// Accounting first: its control accounts are what the others' sub-accounts
    /// hang beneath, and its numbering series is what their codes come from.
    /// </summary>
    private static readonly string[] Services = ["Accounting", "Contacts", "Inventory"];

    public HttpTenantSeeder(
        IHttpClientFactory clients, IConfiguration config, ILogger<HttpTenantSeeder> log)
    {
        _clients = clients;
        _config = config;
        _log = log;
    }

    public async Task<IReadOnlyList<string>> SeedAsync(
        Guid customerId, Guid orgId, CancellationToken ct)
    {
        var failed = new List<string>();
        var request = new SeedOrganizationRequest { CustomerId = customerId, OrgId = orgId };

        foreach (string service in Services)
        {
            string? baseUrl = _config[$"Seeding:{service}"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                // Not configured is not the same as unreachable, but the
                // organization is equally unseeded either way, so it is reported.
                _log.LogWarning("Seeding:{Service} is not configured; {Service} was not seeded.",
                    service, service);
                failed.Add(service);
                continue;
            }

            if (!await SeedOneAsync(service, baseUrl, request, ct))
            {
                failed.Add(service);
            }
        }

        return failed;
    }

    private async Task<bool> SeedOneAsync(
        string service, string baseUrl, SeedOrganizationRequest request, CancellationToken ct)
    {
        HttpClient client = _clients.CreateClient("seeding");
        client.BaseAddress = new Uri(baseUrl);

        var message = new HttpRequestMessage(HttpMethod.Post, "internal/seed/organization")
        {
            Content = JsonContent.Create(request),
        };

        string? key = _config["Internal:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
        {
            message.Headers.TryAddWithoutValidation(InternalOnlyAttribute.HeaderName, key);
        }

        try
        {
            HttpResponseMessage response = await client.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogError(
                    "Seeding {Service} for organization {OrgId} returned {Status}.",
                    service, request.OrgId, (int)response.StatusCode);
                return false;
            }

            SeedOrganizationResponse? result =
                await response.Content.ReadFromJsonAsync<SeedOrganizationResponse>(ct);

            _log.LogInformation(
                "Seeded {Service} for organization {OrgId}: {@Seeded}",
                service, request.OrgId, result?.Seeded);

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Every seed is idempotent, so a retry of the whole fan-out is safe.
            _log.LogError(ex, "Seeding {Service} for organization {OrgId} failed.",
                service, request.OrgId);
            return false;
        }
    }
}

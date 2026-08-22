using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Master.Api.Services;

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
    private readonly IServiceProvider _services;
    private readonly ILogger<HttpTenantSeeder> _log;

    /// <summary>
    /// Accounting first: its control accounts are what the others' sub-accounts
    /// hang beneath, and it owns the numbering-series table the rest write into.
    /// Sales seeds its own document series — quote, order, invoice, credit note
    /// and POS sale — so it comes after Accounting has created the table's rows
    /// for the branch, not before.
    ///
    /// Two names have left this list. Banking's series — spend, receive and
    /// transfer money — are Accounting's now that the two are one service.
    /// Contacts is this service, and calling ourselves over HTTP to seed a table
    /// we hold would be a round trip to localhost; it is seeded in process, at
    /// the end, by <see cref="SeedContactRolesAsync"/>.
    /// </summary>
    private static readonly string[] Services = ["Accounting", "Inventory", "Sales"];

    /// <summary>
    /// The branch's current vertical, or General when the row cannot be read.
    /// General is the everything default, so seeding under it is never the
    /// wrong shape — only possibly wider than the branch's own trade.
    /// </summary>
    private async Task<string> ReadVerticalAsync(
        Guid customerId, Guid orgId, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Repository.AdminDbContext>();
            return (await db.Organizations
                    .Where(o => o.CustomerId == customerId && o.OrgId == orgId)
                    .Select(o => (Entity.Enums.Vertical?)o.Vertical)
                    .FirstOrDefaultAsync(ct))
                ?.ToString() ?? "General";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex,
                "Reading vertical for organization {OrgId} failed; seeding as General.",
                orgId);
            return "General";
        }
    }

    public HttpTenantSeeder(
        IHttpClientFactory clients,
        IConfiguration config,
        IServiceProvider services,
        ILogger<HttpTenantSeeder> log)
    {
        _clients = clients;
        _config = config;
        _services = services;
        _log = log;
    }

    public async Task<IReadOnlyList<string>> SeedAsync(
        Guid customerId, Guid orgId, CancellationToken ct)
    {
        var failed = new List<string>();

        // The vertical is read at seed time, not passed in, so a retry after a
        // partial seed and a re-seed after a vertical change both carry the
        // branch's current trade without the callers having to know.
        string vertical = await ReadVerticalAsync(customerId, orgId, ct);

        var request = new SeedOrganizationRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            Vertical = vertical,
        };

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

        if (!await SeedContactRolesAsync(customerId, orgId, ct))
        {
            failed.Add("Contacts");
        }

        return failed;
    }

    /// <summary>
    /// The contact person roles, written straight into the customer's database.
    ///
    /// The tenant is set on the scope before anything resolves a context, for the
    /// same reason the internal endpoint does it: the contacts context is built
    /// from the tenant, so resolving the service first would bind it to none.
    /// The endpoint stays for callers outside this service, and both go through
    /// the same idempotent seeder.
    /// </summary>
    private async Task<bool> SeedContactRolesAsync(
        Guid customerId, Guid orgId, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _services.CreateScope();

            var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenant.CustomerId = customerId;
            tenant.OrgId = orgId;

            var roles = scope.ServiceProvider.GetRequiredService<ContactPersonRoleService>();
            int seeded = await roles.SeedForOrganizationAsync(orgId, ct);

            _log.LogInformation(
                "Seeded Contacts for organization {OrgId}: {Seeded} contact person roles.",
                orgId, seeded);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Idempotent, so a retry of the whole fan-out is safe.
            _log.LogError(ex, "Seeding Contacts for organization {OrgId} failed.", orgId);
            return false;
        }
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

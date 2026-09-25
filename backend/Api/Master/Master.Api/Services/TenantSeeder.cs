using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
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
    /// <summary>
    /// Seeds every service the customer's licensed apps need (TK-45), reading
    /// the licences. Returns the services that could not be reached.
    /// </summary>
    Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct);

    /// <summary>
    /// Seeds the services <paramref name="apps"/> need. For a caller whose new
    /// licence is not committed yet, and so cannot be read by the seeder's own
    /// scope: starting a trial passes the apps it is about to hold.
    /// </summary>
    Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, App apps, CancellationToken ct);
}

public sealed class HttpTenantSeeder : ITenantSeeder
{
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _config;
    private readonly IServiceProvider _services;
    private readonly ILogger<HttpTenantSeeder> _log;

    /// <summary>School's own services (S1 onward), in seeding order: each is added as its stage is built.</summary>
    public static readonly string[] SchoolServices = ["Sis", "Admission"];

    // After SchoolServices, which it spreads: static fields initialise in order.

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
    ///
    /// Purchase seeds its document series — order, goods receipt, bill and
    /// debit note — into the same table as Sales, so it too comes after
    /// Accounting. Reporting seeds the branch's report catalog. Both endpoints
    /// existed long before anything called them, so a branch came up unable to
    /// number a purchase document or list a report (TK-01).
    ///
    /// Printing seeds each branch's print templates, one default per printable
    /// document type. It reads nothing the others write, so its place in the
    /// order does not matter (TK-81).
    ///
    /// Customer seeds each branch's SLA policies, one per ticket priority. It
    /// too reads nothing the others write (TK-18).
    /// </summary>
    private static readonly string[] Services =
        ["Accounting", "Hrm", "TimeLeave", "Payroll", .. SchoolServices, "Inventory", "Sales", "Purchase", "Reporting", "Printing", "Customer"];

    /// <summary>
    /// Which services a set of apps needs, in seeding order (H0.4, TK-45).
    ///
    /// <b>Accounting always</b>, because payroll and school fees post to the
    /// ledger and every app numbers its documents from Accounting's table.
    /// <b>Printing always</b>, because payslips and fee receipts are templates
    /// like invoices. The trading services are RetailErp's. HRMS, Payroll and
    /// School add their own services here as they are built (TK-48 adds Hrm).
    /// </summary>
    public static IReadOnlyList<string> ServicesFor(App apps)
    {
        HashSet<string> wanted = ["Accounting", "Printing"];

        // The employee master (TK-48), after Accounting because its EMP
        // series goes into Accounting's numbering table.
        if ((apps & (App.Hrms | App.Payroll | App.School)) != 0)
        {
            wanted.Add("Hrm");
        }

        if (apps.HasFlag(App.Hrms))
        {
            wanted.Add("TimeLeave");
        }

        if (apps.HasFlag(App.Payroll))
        {
            wanted.Add("Payroll");
        }

        if (apps.HasFlag(App.School))
        {
            wanted.UnionWith(SchoolServices);
        }

        if (apps.HasFlag(App.RetailErp))
        {
            wanted.UnionWith(["Inventory", "Sales", "Purchase", "Reporting", "Customer"]);
        }

        return Services.Where(wanted.Contains).ToList();
    }

    /// <summary>Contacts (in process) are RetailErp's and School's: customers, vendors, guardians.</summary>
    public static bool SeedsContacts(App apps) => (apps & (App.RetailErp | App.School)) != 0;

    /// <summary>
    /// The apps the customer holds a licence for, read in a scope of its own.
    /// None found reads as RetailErp, which is what every customer was before
    /// apps existed.
    /// </summary>
    private async Task<App> ReadLicensedAppsAsync(Guid customerId, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Repository.AdminDbContext>();
            List<App> held = await db.Licenses
                .Where(l => l.CustomerId == customerId)
                .Select(l => l.App)
                .ToListAsync(ct);

            App apps = held.Aggregate(App.None, (all, one) => all | one);
            return apps == App.None ? App.RetailErp : apps;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The same fallback as the vertical's: RetailErp, which is what every
            // customer was before apps existed, rather than seeding nothing.
            _log.LogError(ex, "Reading licences for customer {CustomerId} failed; seeding as RetailErp.", customerId);
            return App.RetailErp;
        }
    }

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
        Guid customerId, Guid orgId, CancellationToken ct) =>
        await SeedAsync(customerId, orgId, await ReadLicensedAppsAsync(customerId, ct), ct);

    public async Task<IReadOnlyList<string>> SeedAsync(
        Guid customerId, Guid orgId, App apps, CancellationToken ct)
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

        foreach (string service in ServicesFor(apps))
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

        if (SeedsContacts(apps) && !await SeedContactRolesAsync(customerId, orgId, ct))
        {
            failed.Add("Contacts");
        }

        // The starting approval workflows (TK-49), in process: they are
        // Master's own apr tables, so an HTTP call would be to ourselves.
        if (apps.HasFlag(App.Hrms) && !await SeedApprovalDefaultsAsync(customerId, orgId, apps, ct))
        {
            failed.Add("Approvals");
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

            // After Accounting has seeded its chart above, so the walk-in's
            // sub-accounts have control accounts to hang from (TK-17).
            var contacts = scope.ServiceProvider.GetRequiredService<ContactService>();
            int walkIn = await contacts.SeedWalkInAsync(await contacts.BranchCurrencyAsync(ct), ct);

            _log.LogInformation(
                "Seeded Contacts for organization {OrgId}: {Seeded} contact person roles, {WalkIn} walk-in customer.",
                orgId, seeded, walkIn);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Idempotent, so a retry of the whole fan-out is safe.
            _log.LogError(ex, "Seeding Contacts for organization {OrgId} failed.", orgId);
            return false;
        }
    }

    /// <summary>Default approval workflows for the branch's apps, idempotent (TK-49).</summary>
    private async Task<bool> SeedApprovalDefaultsAsync(Guid customerId, Guid orgId, App apps, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _services.CreateScope();

            // Tenant first: the context is built from it.
            var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenant.CustomerId = customerId;
            tenant.OrgId = orgId;

            var workflows = scope.ServiceProvider.GetRequiredService<ApprovalWorkflowService>();
            int added = await workflows.SeedDefaultsAsync(orgId, apps, DateOnly.FromDateTime(DateTime.UtcNow), ct);
            _log.LogInformation("Seeded {Count} approval workflow(s) for organization {OrgId}.", added, orgId);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Seeding approval workflows for organization {OrgId} failed.", orgId);
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

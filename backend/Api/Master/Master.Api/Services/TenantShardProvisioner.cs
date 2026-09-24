using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Services;

/// <summary>
/// Adds a tenant database to the shard registry when the pools are full, or
/// for an Elite customer (H0.5, TK-46).
/// </summary>
public interface ITenantShardProvisioner
{
    /// <summary>
    /// Makes a new shard ready and registers it with room for
    /// <paramref name="capacity"/> customers, or returns null when none can be
    /// made here. Returns the database's name.
    /// </summary>
    Task<string?> ProvisionAsync(PlanTier plan, int capacity, CancellationToken ct);
}

/// <summary>
/// The steps are: pick a database, make sure it exists, migrate every tenant
/// schema into it, then register it. Registration comes last, so nothing is ever
/// allocated to a database that migrations have not reached.
///
/// <b>Which database (D-02, TK-27).</b> Outside Development the app creates no
/// database. Infrastructure creates spares ahead of time and lists them in
/// <c>Sharding:StandbyDatabases</c>: a Bicep <c>flexibleServers/databases</c>
/// resource on Azure, or a <c>CREATE DATABASE</c> by the operator on a single
/// PC. The first one not yet registered is taken. In Development, with no
/// standby left, the next name in the country's sequence (<c>IN000002</c>…) is
/// created.
///
/// <b>Registered in a scope of its own</b>, committed at once rather than in
/// the signup's transaction. A signup that fails later must not leave a
/// migrated database unregistered, and two racing signups must not abort each
/// other's transaction on the registry's key. The one that loses the key race
/// gets the same name back, and both then claim through the allocator's guard.
/// </summary>
public sealed class TenantShardProvisioner : ITenantShardProvisioner
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TenantShardProvisioner> _log;

    public TenantShardProvisioner(
        IServiceScopeFactory scopes,
        IConfiguration config,
        IHostEnvironment environment,
        ILogger<TenantShardProvisioner> log)
    {
        _scopes = scopes;
        _config = config;
        _environment = environment;
        _log = log;
    }

    public async Task<string?> ProvisionAsync(PlanTier plan, int capacity, CancellationToken ct)
    {
        using IServiceScope scope = _scopes.CreateScope();
        AdminDbContext db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

        List<string> registered = await db.TenantDatabases.Select(d => d.DatabaseName).ToListAsync(ct);
        string? name = NextDatabaseName(
            registered,
            _config.GetSection("Sharding:StandbyDatabases").Get<string[]>() ?? [],
            DatabaseMigrationService.MayCreateDatabases(_environment),
            _config["Sharding:Prefix"] ?? "IN");

        if (name is null)
        {
            _log.LogError(
                "Every tenant database is full and no standby database is listed in Sharding:StandbyDatabases. "
                + "Create one through infrastructure and list it.");
            return null;
        }

        string admin = _config.GetConnectionString("AdminDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:AdminDatabase is not configured.");
        string connection = DatabaseMigrationService.TenantConnectionString(admin, name);

        try
        {
            await DatabaseMigrationService.EnsureOrRequireDatabaseAsync(connection, _environment, _log, ct);
            _log.LogInformation("Migrating tenant schemas into new shard {Database}...", name);
            await DatabaseMigrationService.MigrateTenantSchemasAsync(connection, ct);
        }
        catch (InvalidOperationException ex)
        {
            // A listed standby that infrastructure never created. Logged with
            // the reason, and signup answers 503 rather than half-provisioning.
            _log.LogError(ex, "Tenant database {Database} could not be prepared.", name);
            return null;
        }

        db.TenantDatabases.Add(new TenantDatabase
        {
            DatabaseName = name,
            PlanType = plan,
            MaxCustomers = capacity,
            CurrentCustomers = 0,
        });

        try
        {
            await db.SaveChangesAsync(ct);
            _log.LogInformation("Registered tenant database {Database} for {Capacity} customer(s).", name, capacity);
        }
        catch (DbUpdateException)
        {
            // Another signup registered the same database first. It is ready
            // either way; the allocator's guarded claim decides who gets a place.
            _log.LogInformation("Tenant database {Database} was registered by another request.", name);
        }

        return name;
    }

    /// <summary>
    /// The database a new shard goes in: the first listed standby not yet
    /// registered; otherwise, where the app may create databases, the next in
    /// the prefix's sequence; otherwise none.
    /// </summary>
    public static string? NextDatabaseName(
        IReadOnlyCollection<string> registered, IReadOnlyList<string> standby, bool mayCreate, string prefix)
    {
        var taken = registered.ToHashSet(StringComparer.OrdinalIgnoreCase);

        string? spare = standby
            .Select(s => s.Trim())
            .FirstOrDefault(s => s.Length > 0 && !taken.Contains(s));
        if (spare is not null)
        {
            return spare;
        }

        if (!mayCreate)
        {
            return null;
        }

        long highest = registered
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(n => long.TryParse(n[prefix.Length..], out long number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{highest + 1:D6}";
    }
}

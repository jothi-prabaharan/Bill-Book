using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Persistence;
using Shared.Kernel.Seed;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Options for configuring <see cref="TenantMigrationMiddleware{TContext}"/>.
/// </summary>
public class TenantMigrationOptions
{
    /// <summary>
    /// Relative or absolute path to the directory containing JSON seed files.
    /// Defaults to "SeedData" inside the application base directory.
    /// </summary>
    public string SeedDataDirectory { get; set; } = "SeedData";

    /// <summary>
    /// Default fallback database name for Trial or unauthenticated requests.
    /// Defaults to "IN000001".
    /// </summary>
    public string DefaultTrialDatabaseName { get; set; } = "IN000001";

    /// <summary>
    /// Whether to automatically execute the dynamic JSON seed loader after applying migrations.
    /// Defaults to true.
    /// </summary>
    public bool AutoRunJsonSeeder { get; set; } = true;
}

/// <summary>
/// Thread-safe ASP.NET Core middleware that verifies and applies pending EF Core migrations
/// (and executes JSON seed loaders) for individual tenant and trial databases on incoming API requests.
/// 
/// Performance & Concurrency Features:
/// 1. Zero-allocation In-Memory Verification Cache: Once a tenant database + schema is confirmed up-to-date,
///    subsequent requests take a lock-free O(1) fast path with zero database roundtrips.
/// 2. Tenant-Keyed Semaphore Locks: Parallel requests for the same tenant are serialized so only one
///    request migrates while others wait; requests for different tenants run concurrently without contention.
/// 3. Resilient Migration Execution: Integrates with <see cref="StartupMigration"/> for lock waits and transient retry handling.
/// </summary>
/// <typeparam name="TContext">The microservice's isolated DbContext.</typeparam>
public class TenantMigrationMiddleware<TContext> where TContext : DbContext
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMigrationMiddleware<TContext>> _logger;
    private readonly TenantMigrationOptions _options;

    // Fast-path in-memory cache: "DbName:SchemaName" -> True (already migrated and verified)
    private static readonly ConcurrentDictionary<string, bool> VerifiedCache = new(StringComparer.OrdinalIgnoreCase);

    // Tenant-keyed concurrency locks: "DbName:SchemaName" -> SemaphoreSlim(1, 1)
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TenantLocks = new(StringComparer.OrdinalIgnoreCase);

    public TenantMigrationMiddleware(
        RequestDelegate next,
        ILogger<TenantMigrationMiddleware<TContext>> logger,
        TenantMigrationOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options ?? new TenantMigrationOptions();
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantDatabaseResolver databaseResolver,
        IJsonSeedLoader seedLoader)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Resolve target tenant / trial database identifier
        string dbName = ResolveDatabaseName(context);
        string schemaName = typeof(TContext).Name;

        string cacheKey = $"{dbName}:{schemaName}";

        // Fast-path: If this tenant database + schema was already verified for this deployment, skip completely
        if (VerifiedCache.TryGetValue(cacheKey, out bool isVerified) && isVerified)
        {
            await _next(context);
            return;
        }

        // Slow-path: Thread-safe per-tenant migration execution
        SemaphoreSlim tenantLock = TenantLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await tenantLock.WaitAsync(context.RequestAborted);

        try
        {
            // Double-checked locking
            if (VerifiedCache.TryGetValue(cacheKey, out bool doubleChecked) && doubleChecked)
            {
                await _next(context);
                return;
            }

            // Obtain a scoped DbContext instance configured for the resolved tenant
            using IServiceScope scope = context.RequestServices.CreateScope();
            TContext dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

            // Check if there are any pending migrations for this service's schema
            IEnumerable<string> pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(context.RequestAborted);
            List<string> pendingList = pendingMigrations.ToList();

            if (pendingList.Count > 0)
            {
                _logger.LogInformation(
                    "Detected {Count} pending migration(s) for schema '{Schema}' in database '{Database}'. Applying now...",
                    pendingList.Count, schemaName, dbName);

                // Execute migration using resilient timeout and retry policies
                await StartupMigration.MigrateAsync(
                    dbContext.Database,
                    _logger,
                    $"{schemaName}@{dbName}",
                    context.RequestAborted);

                // Run dynamic JSON seed loader post-migration
                if (_options.AutoRunJsonSeeder)
                {
                    string seedDir = Path.IsPathRooted(_options.SeedDataDirectory)
                        ? _options.SeedDataDirectory
                        : Path.Combine(AppContext.BaseDirectory, _options.SeedDataDirectory);

                    await seedLoader.SeedFromDirectoryAsync(dbContext, seedDir, context.RequestAborted);
                }
            }
            else
            {
                _logger.LogDebug(
                    "Database '{Database}' schema '{Schema}' is already up-to-date.",
                    dbName, schemaName);
            }

            // Mark this tenant + schema as verified
            VerifiedCache.TryAdd(cacheKey, true);
        }
        finally
        {
            tenantLock.Release();
        }

        await _next(context);
    }

    /// <summary>
    /// Resolves the database name for the current request across claims, headers, and trial fallbacks.
    /// </summary>
    private string ResolveDatabaseName(HttpContext context)
    {
        // 1. From ITenantContext if already populated
        var tenantContext = context.RequestServices.GetService<ITenantContext>();
        if (!string.IsNullOrWhiteSpace(tenantContext?.DatabaseName))
        {
            return tenantContext.DatabaseName;
        }

        // 2. From JWT Claims (database_name or db_name)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            string? tokenDb = context.User.FindFirst("database_name")?.Value
                              ?? context.User.FindFirst("db_name")?.Value;

            if (!string.IsNullOrWhiteSpace(tokenDb))
            {
                return tokenDb;
            }

            // Check for trial plan or customer tier claim
            string? planClaim = context.User.FindFirst("plan_tier")?.Value
                                ?? context.User.FindFirst("license_type")?.Value;

            if (string.Equals(planClaim, "Trial", StringComparison.OrdinalIgnoreCase))
            {
                return _options.DefaultTrialDatabaseName;
            }
        }

        // 3. From Request Headers (e.g. gateway forwarding or internal proxy)
        if (context.Request.Headers.TryGetValue("X-Database-Name", out var dbHeader) &&
            !string.IsNullOrWhiteSpace(dbHeader))
        {
            return dbHeader.ToString().Trim();
        }

        // 4. Default fallback to Trial database
        return _options.DefaultTrialDatabaseName;
    }

    /// <summary>
    /// Resets the in-memory verification cache (useful during testing or hot-reload).
    /// </summary>
    public static void InvalidateCache()
    {
        VerifiedCache.Clear();
    }
}

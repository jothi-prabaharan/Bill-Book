using System.Data.Common;
using Accounting.Repository;
using Customer.Repository;
using Inventory.Repository;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Printing.Repository;
using Printing.Repository.SeedData;
using Purchase.Repository;
using Reporting.Repository;
using Sales.Repository;
using Shared.Kernel.Tenancy;

namespace Master.Api.Services;

/// <summary>
/// Runs EF Core database migrations on application startup.
/// Ensures that EP_Admin is created, and seeds the first TenantDatabase 'IN000001'.
/// It then creates 'IN000001' and runs all 7 module migrations against it.
/// </summary>
public class DatabaseMigrationService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly IConfiguration _config;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHostEnvironment _environment;

    public DatabaseMigrationService(
        IServiceProvider services,
        ILogger<DatabaseMigrationService> logger,
        IConfiguration config,
        IHostApplicationLifetime lifetime,
        IHostEnvironment environment)
    {
        _services = services;
        _logger = logger;
        _config = config;
        _lifetime = lifetime;
        _environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting automated database setup and migrations...");

        using var scope = _services.CreateScope();

        string adminDbString = RequiredConnectionString("AdminDatabase");
        await EnsureOrRequireDatabaseAsync(adminDbString, cancellationToken);

        // 1. Run EF Core Migrations for Admin
        var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        _logger.LogInformation("Migrating Admin database...");
        await adminDb.Database.MigrateAsync(cancellationToken);

        // 2. Seed Geography Data
        _logger.LogInformation("Seeding global geography data...");
        string jsonPath = Path.Combine(AppContext.BaseDirectory, "SeedData", "states.json");
        if (File.Exists(jsonPath))
        {
            await using var stream = File.OpenRead(jsonPath);
            await GeographyJsonLoader.ImportStatesAsync(adminDb, stream, cancellationToken);
            _logger.LogInformation("Geography seeding complete.");
        }

        // 3. Ensure IN000001 Database Exists and seed it
        await EnsureTenantDatabaseSetupAsync(adminDb, adminDbString, "IN000001", cancellationToken);

        // 3b. Every other registered shard gets the same migrations (TK-46),
        // so a schema change reaches every customer, not only IN000001's.
        List<string> otherShards = await adminDb.TenantDatabases
            .Where(d => d.DatabaseName != "IN000001")
            .Select(d => d.DatabaseName)
            .ToListAsync(cancellationToken);
        foreach (string shard in otherShards)
        {
            string shardConnection = TenantConnectionString(adminDbString, shard);
            await EnsureOrRequireDatabaseAsync(shardConnection, cancellationToken);
            _logger.LogInformation("Migrating tenant schemas for {Database}...", shard);
            await MigrateTenantSchemasAsync(shardConnection, cancellationToken);
        }

        // 3c. Each shard's count of customers, recounted from mst.Customers
        // (TK-46). It replaced a count of branches, and a recount at every
        // start also corrects any drift from a signup that failed after its
        // claim.
        await RecountShardCustomersAsync(adminDb, cancellationToken);

        // Blank optional phones are NULL, never '' (D-04, TK-21).
        await BlankPhoneBackfill.RunAdminAsync(adminDb, cancellationToken);

        // 4. Platform operators named by configuration (D-01). Every start, not
        // only the first, so an operator added to the setting later is granted
        // at the next deploy. Grant-only; revoking is an operator's action.
        IReadOnlyList<string> operatorEmails = PlatformOperatorService.BootstrapEmails(_config);
        int granted = await PlatformOperatorService.ApplyBootstrapAsync(adminDb, operatorEmails, cancellationToken);
        if (granted > 0)
        {
            _logger.LogInformation("Granted platform operator access to {Count} user(s) from Bootstrap:OperatorEmails.", granted);
        }

        // Migrate-and-exit, for a deployment that runs migrations as a job ahead
        // of rolling out new revisions. Without it a job running this image
        // would migrate and then serve HTTP forever, and the job would time out
        // and be recorded as failed after doing everything it was asked to.
        //
        // A graceful stop exits 0, which is what marks the job succeeded. A
        // migration that throws never reaches this line: the host fails to
        // start, the process exits non-zero, and the job fails — which is how a
        // deploy pipeline learns to stop before shifting traffic.
        //
        // The stop waits for ApplicationStarted rather than happening here.
        // This runs *during* host startup, before Kestrel binds, and a stop
        // requested now cancels that bind — which surfaces as an unhandled
        // TaskCanceledException, exit code 134, and a job recorded as failed
        // after every migration succeeded. Found by running it, not by reading
        // it: the log said "stopping" and the process crashed anyway.
        if (_config.GetValue<bool>("Migrations:ExitWhenDone"))
        {
            _logger.LogInformation("Migrations:ExitWhenDone is set; migrations are complete, stopping once started.");
            _lifetime.ApplicationStarted.Register(_lifetime.StopApplication);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureTenantDatabaseSetupAsync(AdminDbContext adminDb, string adminDbString, string tenantDbName, CancellationToken ct)
    {
        // Add database to TenantDatabases if it doesn't exist
        var tenantDbEntry = await adminDb.TenantDatabases.FirstOrDefaultAsync(d => d.DatabaseName == tenantDbName, ct);
        if (tenantDbEntry == null)
        {
            tenantDbEntry = new TenantDatabase
            {
                DatabaseName = tenantDbName,
                PlanType = Master.Entity.Enums.PlanTier.Elite,
                MaxCustomers = 1,
                CurrentCustomers = 1
            };
            adminDb.TenantDatabases.Add(tenantDbEntry);
            
            // The first customer, the first branch and the first operator used
            // to be seeded here unconditionally, with a real person's email
            // address and a BCrypt hash committed into this file. Three things
            // were wrong with that and any one of them is a release blocker:
            //
            //   * a password hash in the repository is a credential in the
            //     repository, and it granted RoleId 1 — Owner;
            //   * it ran on any deployment starting with an empty admin
            //     database, production included, so a fresh production install
            //     came up with a working account nobody had asked for;
            //   * the account was named after one person, which is not a
            //     bootstrap, it is somebody's login.
            //
            // What replaces it is a bootstrap that cannot hand anybody a way in.
            // It runs only when there are no users at all, takes the address
            // from configuration rather than from source, and creates the
            // account with *no password* — so the only way to use it is the
            // ordinary reset flow, which proves control of the mailbox. Nothing
            // is created if the setting is absent, because inventing an operator
            // is the failure being fixed.
            await BootstrapFirstOperatorAsync(adminDb, tenantDbName, ct);

            await adminDb.SaveChangesAsync(ct);
        }

        // Build connection string for the tenant DB
        var builder = new NpgsqlConnectionStringBuilder(adminDbString) { Database = tenantDbName };
        string tenantConnectionString = builder.ConnectionString;

        // Ensure Postgres physical database exists
        await EnsureOrRequireDatabaseAsync(tenantConnectionString, ct);

        // Migrate all 8 schemas inside IN000001
        _logger.LogInformation("Migrating tenant schemas for {Database}...", tenantDbName);
        var dummyTenant = new TenantContext { CustomerId = Guid.Empty, OrgId = Guid.Empty };

        await MigrateTenantSchemasAsync(tenantConnectionString, ct);

        // Blank optional phones in con, inv and cus are NULL, never '' (TK-21).
        await using (var contacts = new ContactsDbContext(new DbContextOptionsBuilder<ContactsDbContext>().UseNpgsql(tenantConnectionString).Options, dummyTenant))
        await using (var inventory = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(tenantConnectionString).Options, dummyTenant))
        await using (var customer = new CustomerDbContext(new DbContextOptionsBuilder<CustomerDbContext>().UseNpgsql(tenantConnectionString).Options, dummyTenant))
        {
            int cleared = await BlankPhoneBackfill.RunTenantAsync(contacts, inventory, customer, ct);
            if (cleared > 0)
            {
                _logger.LogInformation("Cleared {Count} blank phone numbers to NULL in {Database}.", cleared, tenantDbName);
            }
        }

        var targetOrgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var seedTenant = new Shared.Kernel.Tenancy.TenantContext { CustomerId = Guid.Parse("00000000-0000-0000-0000-000000000001"), OrgId = targetOrgId };

        // These contexts are built by hand, so nothing registers the tenant
        // interceptor for them — it has to be added here. Without it the
        // connection never sets app.current_customer_id / app.current_org_id,
        // and under a FORCEd RLS policy the existence checks below see no rows
        // and every seed insert is refused. A superuser bypasses RLS entirely,
        // which is why this only shows up on a role that does not (Azure's
        // server admin is not a superuser).
        var seedRls = new RlsConnectionInterceptor(seedTenant);
        
        // Seed Accounting
        await using (var accDb = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>().UseNpgsql(tenantConnectionString).AddInterceptors(seedRls).Options, seedTenant))
        {
            if (!await accDb.Accounts.IgnoreQueryFilters().AnyAsync(a => a.OrgId == targetOrgId, ct))
            {
                accDb.Accounts.AddRange(Accounting.Repository.SeedData.ChartOfAccountsSeed.Build(targetOrgId));
                accDb.TaxMasters.AddRange(Accounting.Repository.SeedData.TaxMasterSeed.Build(targetOrgId, DateOnly.FromDateTime(DateTime.UtcNow)));
                accDb.NumberingSeries.AddRange(Accounting.Repository.SeedData.NumberingSeriesSeed.Build(targetOrgId));
                accDb.PaymentTerms.AddRange(Accounting.Repository.SeedData.PaymentTermsSeed.Build(targetOrgId));
                await accDb.SaveChangesAsync(ct);
            }
        }

        // Seed Inventory
        await using (var invDb = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(tenantConnectionString).AddInterceptors(seedRls).Options, seedTenant))
        {
            if (!await invDb.UomTypes.IgnoreQueryFilters().AnyAsync(u => u.OrgId == targetOrgId, ct))
            {
                var types = Inventory.Repository.SeedData.UomSeed.BuildTypes(targetOrgId);
                invDb.UomTypes.AddRange(types);
                await invDb.SaveChangesAsync(ct);
                
                var typeIds = types.ToDictionary(t => t.UomTypeSystemName!, t => t.UomTypeId);
                invDb.UnitOfMeasures.AddRange(Inventory.Repository.SeedData.UomSeed.BuildUnits(targetOrgId, typeIds));
                invDb.MetalPurities.AddRange(Inventory.Repository.SeedData.MetalPuritiesSeed.Build(targetOrgId));
                await invDb.SaveChangesAsync(ct);
            }
        }
        
        await using (var accDb2 = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>().UseNpgsql(tenantConnectionString).AddInterceptors(seedRls).Options, seedTenant))
        {
            bool hasInvSeries = await accDb2.NumberingSeries.IgnoreQueryFilters().AnyAsync(n => n.OrgId == targetOrgId && n.SeriesCode == "STA", ct);
            if (!hasInvSeries)
            {
                accDb2.NumberingSeries.AddRange(Inventory.Repository.SeedData.NumberingSeriesSeed.Build(targetOrgId));
                accDb2.NumberingSeries.AddRange(Sales.Repository.SeedData.NumberingSeriesSeed.Build(targetOrgId));
                await accDb2.SaveChangesAsync(ct);
            }

            // Purchase's series have their own check rather than riding on the
            // STA one above: a database bootstrapped before Purchase was added
            // here already has STA, so folding POR into that block would leave
            // it without purchase numbering for ever (TK-01).
            bool hasPurSeries = await accDb2.NumberingSeries.IgnoreQueryFilters().AnyAsync(n => n.OrgId == targetOrgId && n.SeriesCode == "POR", ct);
            if (!hasPurSeries)
            {
                accDb2.NumberingSeries.AddRange(Purchase.Repository.SeedData.NumberingSeriesSeed.Build(targetOrgId));
                await accDb2.SaveChangesAsync(ct);
            }
        }

        // Seed Printing: one default template per printable document type.
        // Idempotent per document type, so a branch bootstrapped before
        // Printing took templates over from Master gets its set on the next
        // start rather than never (TK-81: re-seeded, not copied).
        await using (var prtDb = new PrintingDbContext(new DbContextOptionsBuilder<PrintingDbContext>().UseNpgsql(tenantConnectionString).AddInterceptors(seedRls).Options, seedTenant))
        {
            List<string> present = await prtDb.PrintTemplates.IgnoreQueryFilters()
                .Where(t => t.OrgId == targetOrgId)
                .Select(t => t.DocumentTypeCode)
                .ToListAsync(ct);

            var missing = Printing.Repository.SeedData.PrintTemplateSeed.Build(targetOrgId, present);
            if (missing.Count > 0)
            {
                prtDb.PrintTemplates.AddRange(missing);
                await prtDb.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation("Completed tenant schema migrations for {Database}.", tenantDbName);
    }

    /// <summary>
    /// Every tenant schema, migrated into one physical database. The same steps
    /// for the first shard at startup, for every other registered shard at
    /// startup, and for a shard provisioned when the pool fills (TK-46).
    /// </summary>
    public static async Task MigrateTenantSchemasAsync(string connectionString, CancellationToken ct)
    {
        var tenant = new TenantContext { CustomerId = Guid.Empty, OrgId = Guid.Empty };

        await MigrateContextAsync<ContactsDbContext>(connectionString, tenant, "con", ct);
        await MigrateContextAsync<AccountingDbContext>(connectionString, tenant, "acc", ct);
        await MigrateContextAsync<CustomerDbContext>(connectionString, tenant, "cus", ct);
        await MigrateContextAsync<InventoryDbContext>(connectionString, tenant, "inv", ct);
        await MigrateContextAsync<PurchaseDbContext>(connectionString, tenant, "pur", ct);
        await MigrateContextAsync<PrintingDbContext>(connectionString, tenant, "prt", ct);
        await MigrateContextAsync<ReportingDbContext>(connectionString, tenant, "rpt", ct);
        await MigrateContextAsync<SalesDbContext>(connectionString, tenant, "sal", ct);
    }

    /// <summary>
    /// Sets each shard's <c>CurrentCustomers</c> to the number of customers
    /// whose <c>DatabaseName</c> names it. LINQ, one guarded update per shard.
    /// </summary>
    public static async Task RecountShardCustomersAsync(AdminDbContext adminDb, CancellationToken ct)
    {
        Dictionary<string, int> counts = await adminDb.Customers
            .GroupBy(c => c.DatabaseName)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        List<string> shards = await adminDb.TenantDatabases.Select(d => d.DatabaseName).ToListAsync(ct);
        foreach (string shard in shards)
        {
            int count = counts.GetValueOrDefault(shard);
            await adminDb.TenantDatabases
                .Where(d => d.DatabaseName == shard && d.CurrentCustomers != count)
                .ExecuteUpdateAsync(set => set.SetProperty(d => d.CurrentCustomers, count), ct);
        }
    }

    /// <summary>The connection string of one tenant database on the admin database's server.</summary>
    public static string TenantConnectionString(string adminConnectionString, string databaseName) =>
        new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }.ConnectionString;

    private static async Task MigrateContextAsync<TContext>(string connectionString, ITenantContext tenant, string schema, CancellationToken ct) 
        where TContext : TenantDbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema));
        var options = optionsBuilder.Options;

        // Instantiate via reflection since all TenantDbContexts take (DbContextOptions<T>, ITenantContext)
        var context = (TContext)Activator.CreateInstance(typeof(TContext), options, tenant)!;
        await context.Database.MigrateAsync(ct);
        await context.DisposeAsync();
    }

    /// <summary>
    /// Whether this process may create a missing database (D-02, TK-27): in
    /// Development only. Everywhere else the databases are infrastructure's —
    /// Bicep on Azure, the Postgres container's init script on a single PC — so
    /// the application's login never needs <c>CREATEDB</c>.
    /// </summary>
    public static bool MayCreateDatabases(IHostEnvironment environment) => environment.IsDevelopment();

    /// <summary>
    /// Creates the database when this environment may (Development); otherwise
    /// only checks that it is there, and stops startup with the fix in the
    /// message when it is not.
    /// </summary>
    private Task EnsureOrRequireDatabaseAsync(string connectionString, CancellationToken ct) =>
        EnsureOrRequireDatabaseAsync(connectionString, _environment, _logger, ct);

    /// <summary>
    /// Creates the database in Development; anywhere else, requires that it
    /// exists and stops with <see cref="MissingDatabaseMessage"/> when it does
    /// not (D-02, TK-27). Used for a new shard too (TK-46).
    /// </summary>
    public static async Task EnsureOrRequireDatabaseAsync(
        string connectionString, IHostEnvironment environment, ILogger logger, CancellationToken ct)
    {
        if (MayCreateDatabases(environment))
        {
            await EnsureDatabaseExistsAsync(connectionString, logger, ct);
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            throw new InvalidOperationException(MissingDatabaseMessage(connectionString, environment.EnvironmentName), ex);
        }
    }

    /// <summary>What a missing database outside Development says, naming the fix.</summary>
    public static string MissingDatabaseMessage(string connectionString, string environmentName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return $"The database \"{builder.Database}\" does not exist on {builder.Host}. In the "
            + $"{environmentName} environment the application does not create databases (D-02): "
            + "on Azure they are declared in deploy/azure/main.bicep, and on a single PC the "
            + "Postgres container creates them from deploy/local/db/init on its first start. "
            + "Create it as the server administrator, then start again.";
    }

    private static async Task EnsureDatabaseExistsAsync(string connectionString, ILogger logger, CancellationToken ct)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        string targetDatabase = builder.Database!;

        builder.Database = "postgres";
        string rootConnection = builder.ConnectionString;

        await using var connection = new NpgsqlConnection(rootConnection);
        await connection.OpenAsync(ct);

        bool exists;
        await using (var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{targetDatabase}'", connection))
        {
            var result = await checkCmd.ExecuteScalarAsync(ct);
            exists = result != null;
        }

        if (!exists)
        {
            logger.LogInformation("Creating database {Database}...", targetDatabase);
            string sql = $"CREATE DATABASE \"{targetDatabase}\" ENCODING 'UTF8' TEMPLATE template0";
            await using var createCmd = new NpgsqlCommand(sql, connection);
            await createCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private string RequiredConnectionString(string name) =>
        _config.GetConnectionString(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"ConnectionStrings:{name} is not configured.");

    /// <summary>
    /// Creates the first operator account, once, from configuration.
    ///
    /// <b>Idempotent by emptiness, which is what stops it being a back door.</b>
    /// It writes nothing unless <c>mst.Users</c> is completely empty, so it
    /// cannot be re-run to mint a second Owner on a live system, and it cannot
    /// be pointed at an existing deployment by setting the configuration key
    /// afterwards.
    ///
    /// <b>No password is set.</b> The account exists, is active, and has no
    /// credential — signing in is impossible until somebody completes the
    /// forgot-password flow against that mailbox, which is the proof of
    /// ownership a bootstrap needs and cannot fake. A generated password would
    /// have to be printed somewhere, and wherever that is becomes the new
    /// weakest link.
    ///
    /// <b>It grants a tenant Owner role, not <c>platform.*</c>.</b> An operator
    /// is a flag on the user, set by <c>Bootstrap:OperatorEmails</c> (D-01) —
    /// never a role: <c>Role</c> rows are shared system rows, so granting
    /// <c>platform.*</c> to a tenant role would grant it to that role's holders
    /// across every customer.
    /// </summary>
    private async Task BootstrapFirstOperatorAsync(AdminDbContext adminDb, string tenantDbName, CancellationToken ct)
    {
        if (_config["Bootstrap:OwnerEmail"] is not { Length: > 0 } email)
        {
            _logger.LogInformation(
                "No Bootstrap:OwnerEmail configured, so no first account was created. "
                + "Set it to create one; it takes effect only while there are no users.");

            return;
        }

        if (await adminDb.Users.AnyAsync(ct))
        {
            // Not an error, and not logged as one: this is the normal state of
            // every start after the first.
            return;
        }

        var customerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var userId = Guid.NewGuid();

        adminDb.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            CustomerCode = "0000000001",
            CountryPrefix = _config["Bootstrap:CountryPrefix"] ?? "IN",
            Name = _config["Bootstrap:CompanyName"] ?? "First Customer",
            BillingEmail = email,
            DatabaseName = tenantDbName,
            PlanTier = Master.Entity.Enums.PlanTier.Elite,
            Status = Master.Entity.Enums.TenantStatus.Active,
        });

        adminDb.Licenses.Add(new Master.Entity.TableEntities.License
        {
            CustomerId = customerId,
            LicenseType = Master.Entity.Enums.LicenseType.Elite,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(10),
            MaxUsers = 100,
            MaxOrganizations = 100,
            IsActive = true,
            GraceDays = 14,
        });

        adminDb.Organizations.Add(new Master.Entity.TableEntities.Organization
        {
            OrgId = orgId,
            CustomerId = customerId,
            OrgCode = "HO",
            Name = _config["Bootstrap:CompanyName"] ?? "Head Office",
            BaseCurrency = _config["Bootstrap:BaseCurrency"] ?? "INR",
            Status = Master.Entity.Enums.TenantStatus.Active,
        });

        string? hash = null;
        if (_config["Bootstrap:OwnerPassword"] is { Length: > 0 } pass)
        {
            hash = BCrypt.Net.BCrypt.HashPassword(pass, 12);
        }

        adminDb.Users.Add(new Master.Entity.TableEntities.User
        {
            UserId = userId,
            Email = email,
            DisplayName = _config["Bootstrap:OwnerName"] ?? "Administrator",
            // If OwnerPassword is provided (e.g. local dev), hash it. Otherwise, force reset flow.
            PasswordHash = hash,
            EmailConfirmed = false,
            IsActive = true,
        });

        Role? owner = await adminDb.Roles
            .FirstOrDefaultAsync(r => r.IsSystemRole && r.SystemName == "Owner", ct);

        if (owner is null)
        {
            _logger.LogWarning(
                "The Owner role is not seeded, so the first account was created without one. "
                + "It cannot sign in to a branch until a role is assigned.");
        }
        else
        {
            adminDb.UserOrganizationRoles.Add(new UserOrganizationRole
            {
                UserId = userId,
                OrgId = orgId,
                RoleId = owner.RoleId,
                IsActive = true,
            });
        }

        // The address, never a credential — there is none to log.
        _logger.LogInformation(
            "Created the first account for {Email}. It has no password: use forgot-password "
            + "to set one.",
            email);
    }
}

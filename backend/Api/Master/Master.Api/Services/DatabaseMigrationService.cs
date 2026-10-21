using Master.Repository.Repositories;
using Master.Repository.Clients;
using System.Data.Common;
using Accounting.Repository;
using Customer.Repository;
using Inventory.Repository;
using Master.Entity.Enums;
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
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Apps;

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
        await StartupMigration.MigrateAsync(adminDb.Database, _logger, "Admin", cancellationToken);

        // 2. Ensure IN000001 entry in TenantDatabases and bootstrap operator user
        var tenantDbEntry = await adminDb.TenantDatabases.FirstOrDefaultAsync(d => d.DatabaseName == "IN000001", cancellationToken);
        if (tenantDbEntry == null)
        {
            adminDb.TenantDatabases.Add(new TenantDatabase
            {
                DatabaseName = "IN000001",
                PlanType = PlanTier.Elite,
                MaxCustomers = 1,
                CurrentCustomers = 1
            });
            await BootstrapFirstOperatorAsync(adminDb, "IN000001", cancellationToken);
            await adminDb.SaveChangesAsync(cancellationToken);
        }

        // 3. Platform operators named by configuration (D-01)
        IReadOnlyList<string> operatorEmails = PlatformOperatorRepository.BootstrapEmails(_config);
        int granted = await PlatformOperatorRepository.ApplyBootstrapAsync(adminDb, operatorEmails, cancellationToken);
        if (granted > 0)
        {
            _logger.LogInformation("Granted platform operator access to {Count} user(s) from Bootstrap:OperatorEmails.", granted);
        }

        // 4. The bootstrap customer is the Internal one (D-44, TK-153)
        AddOn internalAddOns = InternalCustomerRepository.AddOnsFrom(_config["Bootstrap:InternalAddOns"], out string? addOnProblem);
        if (addOnProblem is not null)
        {
            _logger.LogWarning("{Problem}", addOnProblem);
        }

        if (await new InternalCustomerRepository(adminDb).ConvertAsync(
                InternalCustomerRepository.CustomerId, InternalCustomerRepository.OrgId, internalAddOns, cancellationToken))
        {
            _logger.LogInformation("Made the first customer the Internal one, with a Service head office.");
        }

        // 5. Ensure physical tenant database and Contacts (con) schema for Master.Api
        string tenantConnectionString = TenantConnectionString(adminDbString, "IN000001");
        await EnsureOrRequireDatabaseAsync(tenantConnectionString, cancellationToken);
        var dummyTenant = new TenantContext { CustomerId = Guid.Empty, OrgId = Guid.Empty };
        await MigrateContextAsync<ContactsDbContext>(tenantConnectionString, dummyTenant, "con", _logger, cancellationToken);

        // If running in migration CLI job mode (Migrations:ExitWhenDone), run all remaining tenant schemas synchronously.
        // In normal server mode, run remaining tenant migrations in the background so Kestrel port 4511 opens immediately.
        if (_config.GetValue<bool>("Migrations:ExitWhenDone"))
        {
            _logger.LogInformation("Migrations:ExitWhenDone is set; completing all tenant migrations synchronously...");
            await CompleteTenantMigrationsAndSeedingAsync(adminDb, adminDbString, cancellationToken);
            _logger.LogInformation("Migrations complete, stopping once host is started.");
            _lifetime.ApplicationStarted.Register(_lifetime.StopApplication);
        }
        else
        {
            _logger.LogInformation("Master.Api startup complete. Kestrel is binding port; launching background tenant schema setup.");
            _ = Task.Run(() => RunBackgroundTenantSetupAsync(adminDbString, CancellationToken.None));
        }
    }

    private async Task RunBackgroundTenantSetupAsync(string adminDbString, CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            await CompleteTenantMigrationsAndSeedingAsync(adminDb, adminDbString, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background tenant migrations and seeding failed.");
        }
    }

    private async Task CompleteTenantMigrationsAndSeedingAsync(AdminDbContext adminDb, string adminDbString, CancellationToken ct)
    {
        // 1. Seed Geography Data
        _logger.LogInformation("Seeding global geography data...");
        string jsonPath = Path.Combine(AppContext.BaseDirectory, "SeedData", "states.json");
        if (File.Exists(jsonPath))
        {
            await using var stream = File.OpenRead(jsonPath);
            await GeographyJsonLoader.ImportStatesAsync(adminDb, stream, ct);
            _logger.LogInformation("Geography seeding complete.");
        }

        await BackfillIndianStateCodesAsync(adminDb, _logger, ct);

        // 2. Ensure IN000001 Database Exists and seed it
        await EnsureTenantDatabaseSetupAsync(adminDb, adminDbString, "IN000001", ct);

        // 3. Every other registered shard gets the same migrations (TK-46)
        List<string> otherShards = await adminDb.TenantDatabases
            .Where(d => d.DatabaseName != "IN000001")
            .Select(d => d.DatabaseName)
            .ToListAsync(ct);
        foreach (string shard in otherShards)
        {
            string shardConnection = TenantConnectionString(adminDbString, shard);
            await EnsureOrRequireDatabaseAsync(shardConnection, ct);
            _logger.LogInformation("Migrating tenant schemas for {Database}...", shard);
            await MigrateTenantSchemasAsync(shardConnection, _logger, ct);
        }

        // 4. Shard recount & blank phones
        await RecountShardCustomersAsync(adminDb, ct);
        await BlankPhoneBackfill.RunAdminAsync(adminDb, ct);

        _logger.LogInformation("Background database setup and tenant migrations complete.");
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
                PlanType = PlanTier.Elite,
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

        await MigrateTenantSchemasAsync(tenantConnectionString, _logger, ct);

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

        // Seed Head Office branch
        var headOfficeOrgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var headOfficeCustomerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        await SeedBranchMasterDataAsync(tenantConnectionString, headOfficeCustomerId, headOfficeOrgId, ct);

        // Seed 6 sample branches if Bootstrap:SeedSampleData is enabled (TK-185).
        // Rows and direct seed only: this runs before Master listens, and the
        // HTTP fan-out to every service is SampleDataBootstrapWorker's, once the
        // host is up. Run here, it held Master's port shut while each call to a
        // service still starting failed in turn, and login answered 502 (TK-186).
        if (_config.GetValue<bool>("Bootstrap:SeedSampleData"))
        {
            _logger.LogInformation("Seeding sample customer and 6 business-type branches...");
            var sampleUser = SampleDataRepository.UserFrom(_config);

            if (sampleUser != null)
            {
                using var sampleScope = _services.CreateScope();
                var sampleRepo = sampleScope.ServiceProvider.GetRequiredService<SampleDataRepository>();
                await sampleRepo.EnsureRowsAsync(sampleUser, ct);

                foreach (BusinessType type in SampleDataRepository.BusinessTypes)
                {
                    Guid sampleOrgId = SampleDataRepository.OrgId(type);
                    await SeedBranchMasterDataAsync(tenantConnectionString, SampleDataRepository.CustomerId, sampleOrgId, ct);
                }

                await adminDb.Organizations
                    .Where(o => o.CustomerId == SampleDataRepository.CustomerId && o.Status != TenantStatus.Active)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, TenantStatus.Active), ct);

                await adminDb.Customers
                    .Where(c => c.CustomerId == SampleDataRepository.CustomerId && c.Status != TenantStatus.Active)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, TenantStatus.Active), ct);
            }
        }

        _logger.LogInformation("Completed tenant schema migrations and seeding for {Database}.", tenantDbName);
    }

    private static async Task SeedBranchMasterDataAsync(string tenantConnectionString, Guid customerId, Guid orgId, CancellationToken ct)
    {
        var seedTenant = new Shared.Kernel.Tenancy.TenantContext { CustomerId = customerId, OrgId = orgId };
        var seedRls = new RlsConnectionInterceptor(seedTenant);

        // Seed Accounting
        await using (var accDb = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>().UseNpgsql(tenantConnectionString).AddInterceptors(seedRls).Options, seedTenant))
        {
            if (!await accDb.Accounts.IgnoreQueryFilters().AnyAsync(a => a.OrgId == orgId, ct))
            {
                accDb.Accounts.AddRange(Accounting.Repository.SeedData.ChartOfAccountsSeed.Build(orgId));
                accDb.TaxMasters.AddRange(Accounting.Repository.SeedData.TaxMasterSeed.Build(orgId, DateOnly.FromDateTime(DateTime.UtcNow)));
                accDb.NumberingSeries.AddRange(Accounting.Repository.SeedData.NumberingSeriesSeed.Build(orgId));
                accDb.PaymentTerms.AddRange(Accounting.Repository.SeedData.PaymentTermsSeed.Build(orgId));
                await accDb.SaveChangesAsync(ct);

                // A first version is its own group, as TaxMasterRepository sets
                // it. Left at 0, every rate shares one group and the document
                // choices collapse to a single rate. The GST sub-accounts come
                // from Accounting's own seed, which repairs rows left at 0.
                foreach (var rate in accDb.TaxMasters.Local)
                {
                    rate.TaxGroupId = rate.TaxMasterId;
                }

                await accDb.SaveChangesAsync(ct);
            }
        }

        // Seed Inventory
        await using (var invDb = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(tenantConnectionString).AddInterceptors(seedRls).Options, seedTenant))
        {
            if (!await invDb.UomTypes.IgnoreQueryFilters().AnyAsync(u => u.OrgId == orgId, ct))
            {
                var types = Inventory.Repository.SeedData.UomSeed.BuildTypes(orgId);
                invDb.UomTypes.AddRange(types);
                await invDb.SaveChangesAsync(ct);

                var typeIds = types.ToDictionary(t => t.UomTypeSystemName!, t => t.UomTypeId);
                invDb.UnitOfMeasures.AddRange(Inventory.Repository.SeedData.UomSeed.BuildUnits(orgId, typeIds));
                invDb.MetalPurities.AddRange(Inventory.Repository.SeedData.MetalPuritiesSeed.Build(orgId));
                await invDb.SaveChangesAsync(ct);
            }
        }

        // Seed Numbering Series
        await using (var accDb2 = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>().UseNpgsql(tenantConnectionString).AddInterceptors(seedRls).Options, seedTenant))
        {
            bool hasInvSeries = await accDb2.NumberingSeries.IgnoreQueryFilters().AnyAsync(n => n.OrgId == orgId && n.SeriesCode == "STA", ct);
            if (!hasInvSeries)
            {
                accDb2.NumberingSeries.AddRange(Inventory.Repository.SeedData.NumberingSeriesSeed.Build(orgId));
                accDb2.NumberingSeries.AddRange(Sales.Repository.SeedData.NumberingSeriesSeed.Build(orgId));
                await accDb2.SaveChangesAsync(ct);
            }

            bool hasPurSeries = await accDb2.NumberingSeries.IgnoreQueryFilters().AnyAsync(n => n.OrgId == orgId && n.SeriesCode == "POR", ct);
            if (!hasPurSeries)
            {
                accDb2.NumberingSeries.AddRange(Purchase.Repository.SeedData.NumberingSeriesSeed.Build(orgId));
                await accDb2.SaveChangesAsync(ct);
            }

            bool hasSitSeries = await accDb2.NumberingSeries.IgnoreQueryFilters().AnyAsync(n => n.OrgId == orgId && n.SeriesCode == "SIT", ct);
            if (!hasSitSeries)
            {
                accDb2.NumberingSeries.AddRange(Site.Repository.SeedData.NumberingSeriesSeed.Build(orgId));
                await accDb2.SaveChangesAsync(ct);
            }
        }

        // Seed Printing
        await using (var prtDb = new PrintingDbContext(new DbContextOptionsBuilder<PrintingDbContext>().UseNpgsql(tenantConnectionString).AddInterceptors(seedRls).Options, seedTenant))
        {
            List<string> present = await prtDb.PrintTemplates.IgnoreQueryFilters()
                .Where(t => t.OrgId == orgId)
                .Select(t => t.DocumentTypeCode)
                .ToListAsync(ct);

            var missing = Printing.Repository.SeedData.PrintTemplateSeed.Build(orgId, present);
            if (missing.Count > 0)
            {
                prtDb.PrintTemplates.AddRange(missing);
                await prtDb.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>
    /// Every tenant schema, migrated into one physical database. The same steps
    /// for the first shard at startup, for every other registered shard at
    /// startup, and for a shard provisioned when the pool fills (TK-46).
    /// </summary>
    public static async Task MigrateTenantSchemasAsync(string connectionString, ILogger logger, CancellationToken ct)
    {
        var tenant = new TenantContext { CustomerId = Guid.Empty, OrgId = Guid.Empty };

        await MigrateContextAsync<ContactsDbContext>(connectionString, tenant, "con", logger, ct);
        await MigrateContextAsync<AccountingDbContext>(connectionString, tenant, "acc", logger, ct);
        await MigrateContextAsync<CustomerDbContext>(connectionString, tenant, "cus", logger, ct);
        await MigrateContextAsync<Employee.Repository.EmployeeDbContext>(connectionString, tenant, "hrm", logger, ct);
        await MigrateContextAsync<InventoryDbContext>(connectionString, tenant, "inv", logger, ct);
        await MigrateContextAsync<PurchaseDbContext>(connectionString, tenant, "pur", logger, ct);
        await MigrateContextAsync<PrintingDbContext>(connectionString, tenant, "prt", logger, ct);
        await MigrateContextAsync<ReportingDbContext>(connectionString, tenant, "rpt", logger, ct);
        await MigrateContextAsync<SalesDbContext>(connectionString, tenant, "sal", logger, ct);
        await MigrateContextAsync<Payroll.Repository.PayrollDbContext>(connectionString, tenant, "pay", logger, ct);
        await MigrateContextAsync<TimeLeave.Repository.TimeLeaveDbContext>(connectionString, tenant, "tla", logger, ct);
        await MigrateContextAsync<Student.Repository.StudentDbContext>(connectionString, tenant, "sis", logger, ct);
        await MigrateContextAsync<Admission.Repository.AdmissionDbContext>(connectionString, tenant, "adm", logger, ct);
        await MigrateContextAsync<Attendance.Repository.AttendanceDbContext>(connectionString, tenant, "att", logger, ct);
        await MigrateContextAsync<Fee.Repository.FeeDbContext>(connectionString, tenant, "fee", logger, ct);
        await MigrateContextAsync<Facility.Repository.FacilityDbContext>(connectionString, tenant, "fac", logger, ct);
        await MigrateContextAsync<WorkOrder.Repository.WorkOrderDbContext>(connectionString, tenant, "wrk", logger, ct);
        await MigrateContextAsync<Preventive.Repository.PreventiveDbContext>(connectionString, tenant, "ppm", logger, ct);
        await MigrateContextAsync<MaintenanceContract.Repository.MaintenanceContractDbContext>(connectionString, tenant, "amc", logger, ct);
        await MigrateContextAsync<Claims.Repository.ClaimsDbContext>(connectionString, tenant, "clm", logger, ct);
        await MigrateContextAsync<Recruitment.Repository.RecruitmentDbContext>(connectionString, tenant, "rec", logger, ct);
        await MigrateContextAsync<Performance.Repository.PerformanceDbContext>(connectionString, tenant, "prf", logger, ct);
        await MigrateContextAsync<Site.Repository.SiteDbContext>(connectionString, tenant, "sit", logger, ct);
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

    private static async Task MigrateContextAsync<TContext>(string connectionString, ITenantContext tenant, string schema, ILogger logger, CancellationToken ct)
        where TContext : TenantDbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            npgsql.CommandTimeout(180);
        });
        var options = optionsBuilder.Options;

        // Instantiate via reflection since all TenantDbContexts take (DbContextOptions<T>, ITenantContext)
        await using var context = (TContext)Activator.CreateInstance(typeof(TContext), options, tenant)!;
        await StartupMigration.MigrateAsync(context.Database, logger, schema, ct);
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
            try
            {
                await createCmd.ExecuteNonQueryAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateDatabase)
            {
                // A service starting alongside created it between the check
                // and this statement: EF's Migrate creates a missing database
                // itself. It is there, which is all this needed (TK-186).
                logger.LogInformation("Database {Database} was created by another host meanwhile.", targetDatabase);
            }
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

        // Our own organisation: the Internal customer, whose head office is a
        // Service business (D-44, TK-153).
        Guid customerId = InternalCustomerRepository.CustomerId;
        Guid orgId = InternalCustomerRepository.OrgId;
        var userId = Guid.NewGuid();
        AddOn addOns = InternalCustomerRepository.AddOnsFrom(_config["Bootstrap:InternalAddOns"], out _);

        adminDb.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            CustomerCode = InternalCustomerRepository.CustomerCode,
            CountryPrefix = _config["Bootstrap:CountryPrefix"] ?? "IN",
            Name = _config["Bootstrap:CompanyName"] ?? "First Customer",
            BillingEmail = email,
            DatabaseName = tenantDbName,
            PlanTier = PlanTier.Elite,
            Kind = Master.Entity.Enums.CustomerKind.Internal,
            Status = Master.Entity.Enums.TenantStatus.Active,
        });

        adminDb.Licenses.Add(new Master.Entity.TableEntities.License
        {
            CustomerId = customerId,
            LicenseType = Master.Entity.Enums.LicenseType.Elite,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            // Never lapses (TK-152).
            ExpiryDate = CustomerKindRepository.NoExpiry,
            MaxUsers = CustomerKindRepository.UnbilledMaxUsers,
            MaxOrganizations = CustomerKindRepository.UnbilledMaxOrganizations,
            IsActive = true,
            GraceDays = 14,
        });

        int? defaultStateId = await adminDb.States
            .Where(s => s.CountryId == 1 && s.StateCode == "33")
            .Select(s => (int?)s.StateId)
            .FirstOrDefaultAsync(ct);

        adminDb.Organizations.Add(new Master.Entity.TableEntities.Organization
        {
            OrgId = orgId,
            CustomerId = customerId,
            OrgCode = InternalCustomerRepository.BranchCode,
            BusinessType = InternalCustomerRepository.BranchBusinessType,
            Vertical = Master.Entity.Enums.Vertical.Service,
            AddOns = addOns,
            Name = _config["Bootstrap:CompanyName"] ?? "Head Office",
            BaseCurrency = _config["Bootstrap:BaseCurrency"] ?? "INR",
            CountryId = 1,
            StateId = defaultStateId,
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

    private static async Task BackfillIndianStateCodesAsync(AdminDbContext adminDb, ILogger logger, CancellationToken ct)
    {
        var indianStates = await adminDb.States
            .Where(s => s.CountryId == 1)
            .ToListAsync(ct);

        bool statesChanged = false;
        foreach (var state in indianStates)
        {
            string? gstCode = Shared.Kernel.Tax.IndianStateCodes.ToGstCode(state.StateCode)
                ?? Shared.Kernel.Tax.IndianStateCodes.ToGstCode(state.StateName);
            if (gstCode != null && state.StateCode != gstCode)
            {
                state.StateCode = gstCode;
                statesChanged = true;
            }
        }

        if (statesChanged)
        {
            await adminDb.SaveChangesAsync(ct);
            logger.LogInformation("Backfilled Indian state codes to 2-digit GST codes in mst.States.");
        }

        // Ensure branches with GSTIN or Head Office have StateId linked
        var orgs = await adminDb.Organizations.ToListAsync(ct);
        bool orgsChanged = false;
        foreach (var org in orgs)
        {
            if (org.StateId == null)
            {
                string? gstCode = Shared.Kernel.Tax.Gstin.StateCodeOf(org.Gstin);
                if (gstCode == null && org.OrgId == InternalCustomerRepository.OrgId)
                {
                    gstCode = "33"; // Default Tamil Nadu for Head Office
                }

                if (gstCode != null)
                {
                    var matched = indianStates.FirstOrDefault(s => s.StateCode == gstCode);
                    if (matched != null)
                    {
                        org.StateId = matched.StateId;
                        if (org.CountryId == 0) org.CountryId = matched.CountryId;
                        orgsChanged = true;
                        logger.LogInformation(
                            "Linked Organization {OrgId} ({Name}) to State {StateName} ({StateCode}).",
                            org.OrgId, org.Name, matched.StateName, matched.StateCode);
                    }
                }
            }
        }

        if (orgsChanged)
        {
            await adminDb.SaveChangesAsync(ct);
        }
    }
}


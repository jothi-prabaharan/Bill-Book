using System.Data.Common;
using Accounting.Repository;
using Customer.Repository;
using Inventory.Repository;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public DatabaseMigrationService(IServiceProvider services, ILogger<DatabaseMigrationService> logger, IConfiguration config)
    {
        _services = services;
        _logger = logger;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting automated database setup and migrations...");

        using var scope = _services.CreateScope();

        string adminDbString = RequiredConnectionString("AdminDatabase");
        await EnsureDatabaseExistsAsync(adminDbString, cancellationToken);

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
                PlanType = "Elite",
                MaxOrganizations = 1,
                CurrentOrganizations = 1
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
        await EnsureDatabaseExistsAsync(tenantConnectionString, ct);

        // Migrate all 7 schemas inside IN000001
        _logger.LogInformation("Migrating tenant schemas for {Database}...", tenantDbName);
        var dummyTenant = new TenantContext { CustomerId = Guid.Empty, OrgId = Guid.Empty };

        await MigrateContextAsync<ContactsDbContext>(tenantConnectionString, dummyTenant, "con", ct);
                        await MigrateContextAsync<AccountingDbContext>(tenantConnectionString, dummyTenant, "acc", ct);
        await MigrateContextAsync<CustomerDbContext>(tenantConnectionString, dummyTenant, "cus", ct);
        await MigrateContextAsync<InventoryDbContext>(tenantConnectionString, dummyTenant, "inv", ct);
        await MigrateContextAsync<PurchaseDbContext>(tenantConnectionString, dummyTenant, "pur", ct);
        await MigrateContextAsync<ReportingDbContext>(tenantConnectionString, dummyTenant, "rpt", ct);
        await MigrateContextAsync<SalesDbContext>(tenantConnectionString, dummyTenant, "sal", ct);
        
        var targetOrgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var seedTenant = new Shared.Kernel.Tenancy.TenantContext { CustomerId = Guid.Parse("00000000-0000-0000-0000-000000000001"), OrgId = targetOrgId };
        
        // Seed Accounting
        await using (var accDb = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>().UseNpgsql(tenantConnectionString).Options, seedTenant))
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
        await using (var invDb = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(tenantConnectionString).Options, seedTenant))
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
        
        await using (var accDb2 = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>().UseNpgsql(tenantConnectionString).Options, seedTenant))
        {
            bool hasInvSeries = await accDb2.NumberingSeries.IgnoreQueryFilters().AnyAsync(n => n.OrgId == targetOrgId && n.SeriesCode == "STA", ct);
            if (!hasInvSeries)
            {
                accDb2.NumberingSeries.AddRange(Inventory.Repository.SeedData.NumberingSeriesSeed.Build(targetOrgId));
                accDb2.NumberingSeries.AddRange(Sales.Repository.SeedData.NumberingSeriesSeed.Build(targetOrgId));
                await accDb2.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation("Completed tenant schema migrations for {Database}.", tenantDbName);
    }

    private async Task MigrateContextAsync<TContext>(string connectionString, ITenantContext tenant, string schema, CancellationToken ct) 
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

    private async Task EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct)
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
            _logger.LogInformation("Creating database {Database}...", targetDatabase);
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
    /// <b>It grants a tenant Owner role, not <c>platform.*</c>.</b> How a
    /// platform operator acquires <c>platform.*</c> is still undecided — see
    /// CLAUDE.md's Undecided section — and this deliberately does not settle it
    /// by seeding one: <c>Role</c> rows are shared system rows, so granting
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
            PlanTier = "Elite",
            Status = Master.Entity.Enums.TenantStatus.Active,
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
            hash = BCrypt.Net.BCrypt.EnhancedHashPassword(pass, 12);
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

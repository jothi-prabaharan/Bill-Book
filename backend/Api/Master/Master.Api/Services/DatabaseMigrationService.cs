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
            
            // Also seed the first Customer if not exists
            if (!await adminDb.Customers.AnyAsync(ct))
            {
                var customerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");

                adminDb.Customers.Add(new Master.Entity.TableEntities.Customer
                {
                    CustomerId = customerId,
                    CustomerCode = "0000000001",
                    Name = "Eternal Pathway Private Limited",
                    BillingEmail = "jothiprabaharan@gmail.com",
                    DatabaseName = tenantDbName,
                    PlanTier = "Elite",
                    Status = Master.Entity.Enums.TenantStatus.Active
                });

                adminDb.Organizations.Add(new Master.Entity.TableEntities.Organization
                {
                    OrgId = orgId,
                    CustomerId = customerId,
                    OrgCode = "HO",
                    Name = "Eternal Pathway Private Limited",
                    BaseCurrency = "INR"
                });

                adminDb.Users.Add(new Master.Entity.TableEntities.User
                {
                    UserId = userId,
                    Email = "jothiprabaharan@gmail.com",
                    DisplayName = "System Admin",
                    // Empty password allows them to trigger reset/first login flow
                    PasswordHash = "$2a$12$t58FhfJw8WRnwiVWhEdKQ.jwMqbrlrXaMjVvUQuWXGu8nM9Zpznyi", 
                    EmailConfirmed = true,
                    IsActive = true
                });

                adminDb.UserOrganizationRoles.Add(new Master.Entity.TableEntities.UserOrganizationRole
                {
                    UserId = userId,
                    OrgId = orgId,
                    RoleId = 1, // Owner Role
                    IsActive = true
                });
            }

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
}

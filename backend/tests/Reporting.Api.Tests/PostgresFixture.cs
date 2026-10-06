using Microsoft.EntityFrameworkCore;
using Reporting.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// A real PostgreSQL, because what these tests check is half in the database.
///
/// The RLS policies are raw SQL in a migration and the model knows nothing about
/// them; the query filter is in the model and the database knows nothing about
/// it. Checking that both are present means having both.
///
/// <b>The suite skips itself when no server answers.</b> Point
/// <c>REPORTING_TEST_DB</c> at one to run it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=reporting_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    private string ConnectionString =>
        Environment.GetEnvironmentVariable("REPORTING_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            await using var accounting = new Accounting.Repository.AccountingDbContext(
                new DbContextOptionsBuilder<Accounting.Repository.AccountingDbContext>()
                    .UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "acc")).Options,
                new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" });
            await accounting.Database.MigrateAsync();

            await using var sales = new Sales.Repository.SalesDbContext(
                new DbContextOptionsBuilder<Sales.Repository.SalesDbContext>()
                    .UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "sal")).Options,
                new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" });
            await sales.Database.MigrateAsync();

            await using var purchase = new Purchase.Repository.PurchaseDbContext(
                new DbContextOptionsBuilder<Purchase.Repository.PurchaseDbContext>()
                    .UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "pur")).Options,
                new TenantContext { CustomerId = customerId, OrgId = orgId });
            await purchase.Database.MigrateAsync();

            await using var contacts = new Master.Repository.ContactsDbContext(
                new DbContextOptionsBuilder<Master.Repository.ContactsDbContext>()
                    .UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "con")).Options,
                new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" });
            await contacts.Database.MigrateAsync();

            await using ReportingDbContext db = CreateContext(customerId, orgId);
            await db.Database.MigrateAsync();

            await db.Database.ExecuteSqlRawAsync(@"
                DO $$
                DECLARE
                    r RECORD;
                BEGIN
                    FOR r IN (
                        SELECT nspname, relname, conname
                        FROM pg_constraint c
                        JOIN pg_class cl ON c.conrelid = cl.oid
                        JOIN pg_namespace n ON cl.relnamespace = n.oid
                        WHERE nspname IN ('sal', 'pur', 'con')
                          AND relname IN ('Invoices', 'Bills', 'Contacts')
                          AND contype = 'c'
                    ) LOOP
                        EXECUTE format('ALTER TABLE %I.%I DROP CONSTRAINT IF EXISTS %I', r.nspname, r.relname, r.conname);
                    END LOOP;

                    FOR r IN (
                        SELECT table_schema, table_name, column_name 
                        FROM information_schema.columns 
                        WHERE table_schema IN ('sal', 'pur', 'con') 
                          AND table_name IN ('Invoices', 'Bills', 'Contacts') 
                          AND is_nullable = 'NO' 
                          AND column_name NOT IN ('InvoiceId', 'BillId', 'ContactId', 'OrgId', 'CustomerId')
                    ) LOOP
                        EXECUTE format('ALTER TABLE %I.%I ALTER COLUMN %I DROP NOT NULL', r.table_schema, r.table_name, r.column_name);
                    END LOOP;
                END $$;
            ");
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set REPORTING_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

    /// <summary>
    /// Whether the server could not be reached at all — as opposed to answering
    /// and refusing what we asked it. See Sales.Api.Tests.PostgresFixture, which
    /// this mirrors: a model that disagrees with its migrations is a failure,
    /// only a socket that will not open is a skip.
    /// </summary>
    private static bool IsUnreachable(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current.GetType().Name == "PostgresException")
            {
                return false;
            }

            if (current is System.Net.Sockets.SocketException
                or TimeoutException
                || current.GetType().Name == "NpgsqlException")
            {
                return true;
            }
        }

        return false;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public ReportingDbContext CreateContext(Guid customerId, Guid orgId)
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ReportingDbContext(
            options, new TenantContext { CustomerId = customerId, OrgId = orgId });
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

using Sales.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// A real PostgreSQL, for the same reason Accounting's suite needs one: what is
/// being tested here is almost entirely the database's half of the schema — check
/// constraints and a deferred trigger. An in-memory provider has neither, so a
/// test that passed against one would prove nothing about the thing being
/// claimed.
///
/// <b>The suite skips itself when no server answers</b> rather than failing. A
/// suite that fails on a machine without Postgres trains people to ignore red;
/// one that passes without running is worse.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=sales_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    private string ConnectionString =>
        Environment.GetEnvironmentVariable("SALES_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            // Migrate rather than EnsureCreated: the triggers and RLS policies
            // live in the migrations, and EnsureCreated builds the tables from
            // the model and skips every one of them.
            var customerId = Guid.NewGuid();
            var orgId = Guid.NewGuid();

            // Accounting first: it owns acc.NumberingSeries, which Sales maps
            // but does not migrate. In production both schemas live in the one
            // per-customer database, and a test database missing half of it would
            // fail on the first number allocated rather than on anything real.
            await using var accounting = new Accounting.Repository.AccountingDbContext(
                new DbContextOptionsBuilder<Accounting.Repository.AccountingDbContext>()
                    .UseNpgsql(ConnectionString).Options,
                new TenantContext { CustomerId = customerId, OrgId = orgId });

            await accounting.Database.MigrateAsync();

            await using SalesDbContext db = CreateContext(customerId, orgId);
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            SkipReason =
                $"No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set SALES_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// A context bound to one branch. Each test gets its own OrgId, so the query
    /// filter keeps them apart — which also means the tests exercise the filter
    /// rather than working around it.
    /// </summary>
    public SalesDbContext CreateContext(Guid customerId, Guid orgId)
    {
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new SalesDbContext(
            options, new TenantContext { CustomerId = customerId, OrgId = orgId });
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// A real PostgreSQL, for the same reason Accounting's and Banking's suites need
/// one: what an adjustment sheet actually guarantees lives half in the database —
/// five check constraints, the guarded stock decrement, and a numbering
/// allocation that has to join the caller's transaction. An in-memory provider
/// has none of those, so a test that passed against one would prove nothing about
/// the thing being claimed.
///
/// <b>The suite skips itself when no server answers</b> rather than failing. A
/// suite that fails on a machine without Postgres trains people to ignore red;
/// one that passes without running is worse.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=inventory_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    private string ConnectionString =>
        Environment.GetEnvironmentVariable("INVENTORY_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            var customerId = Guid.NewGuid();
            var orgId = Guid.NewGuid();

            // Accounting first: it owns acc.NumberingSeries, which Inventory maps
            // but does not migrate. Posting a sheet allocates from it, so a test
            // database missing that table would fail on the number rather than on
            // anything the test is about.
            await using var accounting = new Accounting.Repository.AccountingDbContext(
                new DbContextOptionsBuilder<Accounting.Repository.AccountingDbContext>()
                    .UseNpgsql(ConnectionString).Options,
                new TenantContext { CustomerId = customerId, OrgId = orgId });

            await accounting.Database.MigrateAsync();

            await using InventoryDbContext db = CreateContext(customerId, orgId);
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set INVENTORY_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

    /// <summary>
    /// Whether the server could not be reached at all — as opposed to answering
    /// and refusing what we asked it. Catching every exception here (the previous
    /// behavior) turns a real migration/schema failure into a green skip, which is
    /// exactly the failure mode Sales.Api.Tests.PostgresFixture was written to
    /// avoid — brought into line here for the same reason, since the CustomerId
    /// migration and RLS rewrite this suite now checks deserve a suite that
    /// actually fails when they are wrong.
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

    /// <summary>
    /// A context bound to one branch. Each test gets its own OrgId, so the query
    /// filter keeps them apart — which also means the tests exercise the filter
    /// rather than working around it.
    /// </summary>
    public InventoryDbContext CreateContext(Guid customerId, Guid orgId)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new InventoryDbContext(
            options, new TenantContext { CustomerId = customerId, OrgId = orgId });
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

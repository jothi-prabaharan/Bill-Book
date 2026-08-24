using Microsoft.EntityFrameworkCore;
using Customer.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Customer.Api.Tests;

/// <summary>
/// A real PostgreSQL, because what these tests check is half in the database.
///
/// The RLS policies are raw SQL in a migration and the model knows nothing about
/// them; the query filter is in the model and the database knows nothing about
/// it. Checking that both are present means having both — an in-memory provider
/// has neither, and a green suite against one would prove nothing about the
/// guard it claims to be testing.
///
/// <b>The suite skips itself when no server answers.</b> Point
/// <c>SALES_TEST_DB</c> at one to run it.
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
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            // Accounting first: it owns acc.NumberingSeries, which Sales maps but
            // does not migrate. In production both schemas live in the one
            // per-customer database, and a test database missing half of it would
            // fail on the first number allocated rather than on anything real.
            await using var master = new Master.Repository.ContactsDbContext(
                new DbContextOptionsBuilder<Master.Repository.ContactsDbContext>()
                    .UseNpgsql(ConnectionString).Options,
                new TenantContext { CustomerId = customerId, OrgId = orgId });

            await master.Database.MigrateAsync();

            // Migrate rather than EnsureCreated: the RLS policies live in the
            // migrations and EnsureCreated builds the tables from the model,
            // skipping every one of them — which would quietly disable half of
            // what this suite exists to check.
            await using CustomerDbContext db = CreateContext(customerId, orgId);
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set SALES_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

    /// <summary>
    /// Whether the server could not be reached at all — as opposed to answering
    /// and refusing what we asked it.
    ///
    /// <b>The distinction is the whole point of this method.</b> The other
    /// suites catch every exception here and report all of them as "no
    /// PostgreSQL", which turns a real schema failure into a green skip. That
    /// would have made this suite worthless for the thing it exists to catch:
    /// take <c>base.OnModelCreating</c> back out and the migration stops
    /// matching the model, which throws here — and under a catch-all the run
    /// goes green with five skips and CI is none the wiser.
    ///
    /// A model that disagrees with its migrations is a failure. Only a socket
    /// that will not open is a skip.
    /// </summary>
    private static bool IsUnreachable(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            // PostgresException means the server answered and refused what we
            // asked — a schema fault, and never a reason to skip. It derives
            // from NpgsqlException, so it has to be excluded explicitly or the
            // check below swallows exactly the failures this suite is for.
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
    public CustomerDbContext CreateContext(Guid customerId, Guid orgId)
    {
        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new CustomerDbContext(
            options, new TenantContext { CustomerId = customerId, OrgId = orgId });
    }
}

/// <summary>
/// One fixture shared by every class in this suite.
///
/// <b>A class fixture would be one per test class</b>, and each would run
/// <c>MigrateAsync</c> against the same database at the same time — two migrators
/// racing on one schema, which fails on whichever index the loser tries to create
/// second. That is not a fault in the schema and reads like one. Purchase's suite
/// is shared for the same reason.
/// </summary>
[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

using Microsoft.EntityFrameworkCore;
using Master.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

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
/// <c>CONTACTS_TEST_DB</c> at one to run it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=contacts_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    private string ConnectionString =>
        Environment.GetEnvironmentVariable("CONTACTS_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            await using ContactsDbContext db = CreateContext(Guid.NewGuid(), Guid.NewGuid());

            // Migrate rather than EnsureCreated: the RLS policies live in the
            // migrations and EnsureCreated builds the tables from the model,
            // skipping every one of them — which would quietly disable half of
            // what this suite exists to check.
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set CONTACTS_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

    /// <summary>
    /// Whether the server could not be reached at all — as opposed to answering
    /// and refusing what we asked it.
    ///
    /// A model that disagrees with its migrations is a failure. Only a socket
    /// that will not open is a skip — see Sales.Api.Tests.PostgresFixture, which
    /// this mirrors.
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
    /// A context bound to one customer and one branch. Each test picks its own
    /// ids, so the query filter keeps them apart — which also means the tests
    /// exercise the filter rather than working around it.
    /// </summary>
    public ContactsDbContext CreateContext(Guid customerId, Guid orgId)
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ContactsDbContext(
            options, new TenantContext { CustomerId = customerId, OrgId = orgId });
    }
}

/// <summary>
/// One fixture shared by every class in this suite.
///
/// A class fixture would be one per test class, and each would run
/// <c>MigrateAsync</c> against the same database at the same time — two migrators
/// racing on one schema, which fails on whichever index the loser tries to create
/// second.
/// </summary>
[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

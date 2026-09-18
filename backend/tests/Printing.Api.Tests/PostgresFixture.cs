using Microsoft.EntityFrameworkCore;
using Printing.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Printing.Api.Tests;

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
/// <c>PRINTING_TEST_DB</c> at one to run it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=printing_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    private string ConnectionString =>
        Environment.GetEnvironmentVariable("PRINTING_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            // Printing maps one table and reads nobody else's, so unlike the
            // other suites there is no second context to migrate first.
            await using PrintingDbContext db = CreateContext(Guid.NewGuid(), Guid.NewGuid());

            // Migrate rather than EnsureCreated: the RLS policies live in the
            // migration and EnsureCreated builds the tables from the model,
            // skipping every one of them — which would quietly disable half of
            // what this suite exists to check.
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set PRINTING_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

    /// <summary>
    /// Whether the server could not be reached at all — as opposed to answering
    /// and refusing what we asked it.
    ///
    /// <b>The distinction is the whole point of this method.</b> Several suites
    /// catch every exception here and report all of them as "no PostgreSQL",
    /// which turns a real schema failure into a green skip — and a skip reads as
    /// a pass. Take the RLS block back out of the migration and this suite must
    /// go red, not quiet.
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
    public PrintingDbContext CreateContext(Guid customerId, Guid orgId)
    {
        var options = new DbContextOptionsBuilder<PrintingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new PrintingDbContext(
            options, new TenantContext { CustomerId = customerId, OrgId = orgId });
    }
}

/// <summary>
/// One fixture shared by every class in this suite.
///
/// <b>A class fixture would be one per test class</b>, and each would run
/// <c>MigrateAsync</c> against the same database at the same time — two migrators
/// racing on one schema, which fails on whichever index the loser tries to create
/// second.
/// </summary>
[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

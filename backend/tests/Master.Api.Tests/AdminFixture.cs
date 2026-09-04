using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// A real PostgreSQL holding the master database, for the tests that exercise
/// authentication end to end.
///
/// <b>Not a mock, because the guarantees are in the database.</b> Refresh-token
/// rotation turns on a guarded <c>ExecuteUpdate</c> whose row count decides
/// which of two concurrent callers won, and on a unique index over the token
/// hash. An in-memory provider has neither: it would report both callers winning
/// and prove the opposite of what the test claims.
///
/// <b>The suite skips itself when no server answers.</b> Point
/// <c>ADMIN_TEST_DB</c> at one to run it. A server that answers and refuses is a
/// failure, never a skip — see <c>PostgresFixture</c>, which this mirrors.
/// </summary>
public sealed class AdminFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=admin_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    public string ConnectionString =>
        Environment.GetEnvironmentVariable("ADMIN_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            await using AdminDbContext db = CreateContext();
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set ADMIN_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

    private static bool IsUnreachable(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            // The server answered and refused. A schema fault, never a skip.
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

    public AdminDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AdminDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);
}

/// <summary>
/// One fixture per suite. Two classes each migrating the same database at the
/// same time race on whichever index the loser creates second, which fails and
/// reads like a schema fault.
/// </summary>
[CollectionDefinition(nameof(AdminCollection))]
public sealed class AdminCollection : ICollectionFixture<AdminFixture>;

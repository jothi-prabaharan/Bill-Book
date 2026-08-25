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
            await using ReportingDbContext db = CreateContext(Guid.NewGuid(), Guid.NewGuid());
            await db.Database.MigrateAsync();
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

using Fee.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Fee.Api.Tests;

/// <summary>
/// A real PostgreSQL for the <c>fee</c> suite (S4, TK-64): the RLS policies
/// are raw SQL in the migration, so checking them takes a database. Skips only
/// when no server answers; a server that answers and refuses is a failure.
/// Point <c>FEE_TEST_DB</c> at one to run it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=fee_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    public string ConnectionString =>
        Environment.GetEnvironmentVariable("FEE_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            // Accounting first: it owns acc.NumberingSeries, which this
            // service's codes go into.
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
            await using (var accounting = new Accounting.Repository.AccountingDbContext(
                new DbContextOptionsBuilder<Accounting.Repository.AccountingDbContext>()
                    .UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "acc"))
                    .Options,
                tenant))
            {
                await accounting.Database.MigrateAsync();
            }

            await using FeeDbContext db = CreateContext(Guid.NewGuid(), Guid.NewGuid());
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set FEE_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

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

    /// <summary>A context bound to one branch, so each test exercises the query filter.</summary>
    public FeeDbContext CreateContext(Guid customerId, Guid orgId) =>
        new(
            new DbContextOptionsBuilder<FeeDbContext>()
                .UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "fee"))
                .Options,
            new TenantContext { CustomerId = customerId, OrgId = orgId });
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

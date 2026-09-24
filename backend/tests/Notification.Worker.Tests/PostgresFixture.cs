using Microsoft.EntityFrameworkCore;
using Notification.Worker.Persistence;
using Sales.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Notification.Worker.Tests;

/// <summary>
/// A real PostgreSQL, because the dedupe guarantee is the database's: a primary
/// key that lets one claim through and a guarded update whose row count decides
/// a takeover. An in-memory provider has neither.
///
/// <b>The suite skips itself when no server answers.</b> Point
/// <c>NOTIFICATION_TEST_DB</c> at one to run it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=notification_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("NOTIFICATION_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            await using NotificationDbContext db = CreateContext();
            await db.Database.MigrateAsync();

            // The payment reminders read sal, which lives in the same tenant
            // database as ntf in production (TK-20).
            await using SalesDbContext sales = CreateSalesContext(
                new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() });
            await sales.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set NOTIFICATION_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

    /// <summary>Only a socket that will not open is a skip; a server that refuses is a failure.</summary>
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

    /// <summary>A Sales context bound to whatever <paramref name="tenant"/> holds.</summary>
    public SalesDbContext CreateSalesContext(TenantContext tenant) =>
        new(new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "sal"))
            .Options, tenant);

    public NotificationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ntf"))
            .Options);
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

using Microsoft.EntityFrameworkCore;
using Notification.Worker.Persistence;
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

    public NotificationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ntf"))
            .Options);
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

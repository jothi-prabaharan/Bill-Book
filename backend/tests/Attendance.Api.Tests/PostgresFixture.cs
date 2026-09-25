using Attendance.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Attendance.Api.Tests;

/// <summary>
/// A real PostgreSQL for the <c>att</c> suite (S3, TK-63): the RLS policies
/// are raw SQL in the migration, so checking them takes a database. Skips only
/// when no server answers; a server that answers and refuses is a failure.
/// Point <c>ATTENDANCE_TEST_DB</c> at one to run it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=attendance_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    public string ConnectionString =>
        Environment.GetEnvironmentVariable("ATTENDANCE_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            await using AttendanceDbContext db = CreateContext(Guid.NewGuid(), Guid.NewGuid());
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set ATTENDANCE_TEST_DB to point at one. ({ex.GetType().Name})";
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
    public AttendanceDbContext CreateContext(Guid customerId, Guid orgId) =>
        new(
            new DbContextOptionsBuilder<AttendanceDbContext>()
                .UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "att"))
                .Options,
            new TenantContext { CustomerId = customerId, OrgId = orgId });
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

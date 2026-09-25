using Employee.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Employee.Api.Tests;

/// <summary>
/// A real PostgreSQL for the <c>hrm</c> suite (TK-48): the RLS policies are
/// raw SQL in the migration, and the query filter is in the model, so checking
/// both takes both. Skips only when no server answers; a server that answers
/// and refuses is a failure. Point <c>EMPLOYEE_TEST_DB</c> at one to run it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=employee_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    public string ConnectionString =>
        Environment.GetEnvironmentVariable("EMPLOYEE_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            // Accounting first: it owns acc.NumberingSeries, which the EMP
            // series goes into.
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
            await using (var accounting = new Accounting.Repository.AccountingDbContext(
                new DbContextOptionsBuilder<Accounting.Repository.AccountingDbContext>()
                    .UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "acc"))
                    .Options,
                tenant))
            {
                await accounting.Database.MigrateAsync();
            }

            await using EmployeeDbContext db = CreateContext(Guid.NewGuid(), Guid.NewGuid());
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set EMPLOYEE_TEST_DB to point at one. ({ex.GetType().Name})";
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
    public EmployeeDbContext CreateContext(Guid customerId, Guid orgId) =>
        new(
            new DbContextOptionsBuilder<EmployeeDbContext>()
                .UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "hrm"))
                .Options,
            new TenantContext { CustomerId = customerId, OrgId = orgId });
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

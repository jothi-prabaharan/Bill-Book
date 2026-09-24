using Microsoft.EntityFrameworkCore;
using Payroll.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Payroll.Api.Tests;

/// <summary>
/// A real PostgreSQL for the <c>pay</c> suite (TK-51): the RLS policies are
/// raw SQL in the migration, and the query filter is in the model, so checking
/// both takes both. Skips only when no server answers.
/// Point <c>PAYROLL_TEST_DB</c> at one to run it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=payroll_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    public string ConnectionString =>
        Environment.GetEnvironmentVariable("PAYROLL_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            await using PayrollDbContext db = CreateContext(Guid.NewGuid(), Guid.NewGuid());
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set PAYROLL_TEST_DB to point at one. ({ex.GetType().Name})";
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

    public PayrollDbContext CreateContext(Guid customerId, Guid orgId) =>
        new(
            new DbContextOptionsBuilder<PayrollDbContext>()
                .UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "pay"))
                .Options,
            new TenantContext { CustomerId = customerId, OrgId = orgId });
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

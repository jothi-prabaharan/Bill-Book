using Claims.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Claims.Api.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=claims_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    public string ConnectionString =>
        Environment.GetEnvironmentVariable("CLAIMS_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            await using ClaimsDbContext db = CreateContext(Guid.NewGuid(), Guid.NewGuid());
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set CLAIMS_TEST_DB to point at one. ({ex.GetType().Name})";
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

    public ClaimsDbContext CreateContext(Guid customerId, Guid orgId)
    {
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var options = new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new ClaimsDbContext(options, tenant);
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}

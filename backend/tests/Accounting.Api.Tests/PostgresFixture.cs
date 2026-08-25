using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// A real PostgreSQL for the tests that cannot be written against anything else.
///
/// The general ledger's guarantees are half in C# and half in the database: the
/// balance check is a deferred constraint trigger, the replace is an
/// <c>ExecuteDelete</c>, and the numbering allocation is a guarded update that
/// has to join the caller's transaction. An in-memory provider has none of those,
/// so a test that passed against one would prove nothing about the thing being
/// claimed.
///
/// <b>The suite skips itself when no server answers</b> rather than failing.
/// A developer without Postgres running still gets the pure tests, and the
/// database-backed ones say plainly that they did not run.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=accounting_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    private string ConnectionString =>
        Environment.GetEnvironmentVariable("ACCOUNTING_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            // Migrate rather than EnsureCreated: the triggers and the RLS
            // policies live in the migrations, and EnsureCreated builds the
            // tables from the model and skips every one of them.
            await using AccountingDbContext db = CreateContext(Guid.NewGuid(), Guid.NewGuid());
            await db.Database.MigrateAsync();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            SkipReason =
                $"No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set ACCOUNTING_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

    /// <summary>
    /// Whether the server could not be reached at all — as opposed to answering
    /// and refusing what we asked it. Catching every exception here (the previous
    /// behavior) turns a real migration/schema failure into a green skip; brought
    /// into line with Sales.Api.Tests.PostgresFixture for the same reason.
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

    /// <summary>
    /// A context bound to one branch. Each test gets its own OrgId so the query
    /// filter keeps them apart — which also means the tests exercise the filter
    /// rather than working around it.
    /// </summary>
    public AccountingDbContext CreateContext(Guid customerId, Guid orgId)
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AccountingDbContext(
            options, new TenantContext { CustomerId = customerId, OrgId = orgId });
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

/// <summary>The branch's own currency, fixed for tests. Platform is not running here.</summary>
public sealed class StubBaseCurrency(string? currency = "INR") : IBaseCurrencyProvider
{
    public Task<string?> GetBaseCurrencyAsync(CancellationToken ct = default) =>
        Task.FromResult(currency);
}

public sealed class StubFinancialYear(int month = 4) : IFinancialYearProvider
{
    public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(month);
}

public sealed class StubCurrentUser(Guid? userId = null) : ICurrentUser
{
    public Guid? UserId { get; } = userId ?? Guid.NewGuid();

    public Guid? CustomerId => null;

    public Guid? OrgId => null;

    public int? RoleId => null;
}

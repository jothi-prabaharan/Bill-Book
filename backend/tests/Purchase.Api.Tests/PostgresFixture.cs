using Microsoft.EntityFrameworkCore;
using Purchase.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// A real PostgreSQL, for the same reason Accounting's and Inventory's suites
/// need one: what a purchase order actually guarantees lives half in the
/// database. The header and line check constraints, the RLS policies and the
/// numbering allocation — a guarded update that has to join the caller's
/// transaction — are none of them in the model. A test that passed against an
/// in-memory provider would prove nothing about any of it.
///
/// <b>The suite skips itself when no server answers</b> rather than failing. A
/// suite that fails on a machine without Postgres trains people to ignore red;
/// one that passes without running is worse.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=purchase_tests;Username=postgres;Password=123";

    public string? SkipReason { get; private set; }

    private string ConnectionString =>
        Environment.GetEnvironmentVariable("PURCHASE_TEST_DB") ?? DefaultConnection;

    public async Task InitializeAsync()
    {
        try
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            // Accounting first: it owns acc.NumberingSeries, which Purchase maps
            // but does not migrate. Creating an order allocates from it, so a
            // test database missing that table would fail on the number rather
            // than on anything the test is about.
            await using var accounting = new Accounting.Repository.AccountingDbContext(
                new DbContextOptionsBuilder<Accounting.Repository.AccountingDbContext>()
                    .UseNpgsql(ConnectionString).Options,
                new TenantContext { CustomerId = customerId, OrgId = orgId });

            await accounting.Database.MigrateAsync();

            await using PurchaseDbContext db = CreateContext(customerId, orgId);
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            SkipReason =
                "No PostgreSQL answered at the test connection string, so the database-backed "
                + $"tests did not run. Set PURCHASE_TEST_DB to point at one. ({ex.GetType().Name})";
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// A context bound to one branch. Each test gets its own OrgId, so the query
    /// filter keeps them apart — which also means the tests exercise the filter
    /// rather than working around it.
    /// </summary>
    public PurchaseDbContext CreateContext(Guid customerId, Guid orgId)
    {
        var options = new DbContextOptionsBuilder<PurchaseDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new PurchaseDbContext(
            options, new TenantContext { CustomerId = customerId, OrgId = orgId });
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

/// <summary>The branch's own currency, fixed for tests. Master is not running here.</summary>
public sealed class StubBaseCurrency(string? currency = "INR") : IBaseCurrencyProvider
{
    public Task<string?> GetBaseCurrencyAsync(CancellationToken ct = default) =>
        Task.FromResult(currency);
}

/// <summary>
/// The branch's state and its discount rule. <c>33</c> is Tamil Nadu, so a
/// vendor GSTIN starting <c>33</c> is intra-state and anything else is not —
/// which is what decides CGST + SGST against IGST.
/// </summary>
public sealed class StubBranchSettings(string stateCode = "33", bool discountBeforeTax = true)
    : IBranchSettingsProvider
{
    public Task<BranchSettings?> GetSettingsAsync(CancellationToken ct = default) =>
        Task.FromResult<BranchSettings?>(new BranchSettings(stateCode, discountBeforeTax));
}

/// <summary>Branch settings that cannot be read — the transient-failure path.</summary>
public sealed class UnreadableBranchSettings : IBranchSettingsProvider
{
    public Task<BranchSettings?> GetSettingsAsync(CancellationToken ct = default) =>
        Task.FromResult<BranchSettings?>(null);
}

/// <summary>
/// One 18% group, resolved for any date. Real rate resolution is effective-dated
/// and lives in Accounting; what these tests need is that the service asks for a
/// rate and uses the one it is given.
/// </summary>
public sealed class StubTaxRates(decimal percent = 18m) : ITaxRateProvider
{
    /// <summary>A group that has no rate in force, so the refusal path is reachable.</summary>
    public const long UnknownGroupId = 999;

    /// <summary>
    /// Both splits are carried, as the real record does, and the calculator picks
    /// whichever the supply calls for — so one stub serves the intra-state and the
    /// inter-state test alike.
    /// </summary>
    private TaxRate Rate(long taxGroupId) => new(
        TaxMasterId: taxGroupId,
        TaxGroupId: taxGroupId,
        TaxSystemName: $"GST {percent}%",
        TotalRate: percent,
        CgstRate: percent / 2m,
        SgstRate: percent / 2m,
        IgstRate: percent,
        CessRate: 0m);

    public Task<IReadOnlyDictionary<long, TaxRate>?> GetRatesAsync(
        DateOnly onDate, CancellationToken ct = default)
    {
        IReadOnlyDictionary<long, TaxRate> rates = new Dictionary<long, TaxRate>
        {
            [1] = Rate(1),
            [2] = Rate(2),
        };

        return Task.FromResult<IReadOnlyDictionary<long, TaxRate>?>(rates);
    }

    public async Task<TaxRate?> GetRateAsync(
        long taxGroupId, DateOnly onDate, CancellationToken ct = default)
    {
        IReadOnlyDictionary<long, TaxRate>? rates = await GetRatesAsync(onDate, ct);
        return rates is not null && rates.TryGetValue(taxGroupId, out TaxRate? rate) ? rate : null;
    }
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

/// <summary>
/// Names that would come from Master and Inventory. Returns a label for every id
/// asked for, so a test can assert the batching happened without a server.
/// </summary>
public sealed class StubNameLookup : IContactNameLookup, IItemNameLookup
{
    /// <summary>Every id this lookup was asked about, in the order the calls came.</summary>
    public List<IReadOnlyCollection<long>> Calls { get; } = [];

    public Task<IReadOnlyDictionary<long, NamedRef>> ResolveAsync(
        IReadOnlyCollection<long> ids, CancellationToken ct = default)
    {
        Calls.Add(ids);

        IReadOnlyDictionary<long, NamedRef> named = ids.ToDictionary(
            id => id,
            id => new NamedRef(id, $"C{id}", $"Name {id}"));

        return Task.FromResult(named);
    }
}

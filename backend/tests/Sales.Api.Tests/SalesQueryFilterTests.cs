using Microsoft.EntityFrameworkCore;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The guard on the thing <c>sal</c> shipped without.
///
/// <c>SalesDbContext.OnModelCreating</c> never called <c>base.OnModelCreating</c>,
/// so <see cref="TenantDbContext"/> never ran: no table in this schema had an
/// OrgId query filter, an OrgId index or an xmin concurrency token. The RLS
/// policies still refused a cross-branch read at the database, so nothing leaked
/// — but the query filter is the first line of defence and it was missing on
/// every table, and one query written with <c>IgnoreQueryFilters</c> would have
/// gone straight past the only guard that remained.
///
/// It stayed invisible because nothing queried these tables while the schema was
/// being written. By the time it was found there were five controllers reading
/// them. <b>These tests exist so it cannot come back quietly</b>: each one fails
/// the moment that call goes missing again.
///
/// <c>sal.SalesRegister</c> is covered too, and deliberately — it was the one
/// table in the schema with <i>neither</i> guard, having been left out of the RLS
/// loop as well.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SalesQueryFilterTests
{
    private readonly PostgresFixture _postgres;

    public SalesQueryFilterTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Every_org_scoped_sales_entity_has_a_query_filter()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> unfiltered = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters() is not { Count: > 0 })
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        // Asserted over the model rather than by querying, so it names every
        // table that has lost the filter instead of the first one a test happens
        // to touch.
        Assert.Empty(unfiltered);
    }

    [SkippableFact]
    public async Task Every_org_scoped_sales_entity_maps_xmin_as_its_concurrency_token()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> wrong = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.FindProperty(nameof(OrgScopedEntity.Version)) is not { } version
                || version.GetColumnName() != "xmin"
                || !version.IsConcurrencyToken)
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        // Without the base call these mapped to a real column called "Version",
        // which the database duly created and nothing ever wrote to — so every
        // one of these tables had a dead column and no optimistic concurrency.
        Assert.Empty(wrong);
    }

    [SkippableFact]
    public async Task One_branch_cannot_read_another_branchs_quotes()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        await using SalesDbContext mine = _postgres.CreateContext(customerId, Guid.NewGuid());
        await using SalesDbContext theirs = _postgres.CreateContext(customerId, Guid.NewGuid());

        mine.Quotes.Add(new Quote
        {
            TransactionTypeCode = "QTE",
            DocumentNo = $"QT/{Guid.NewGuid():N}"[..20],
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            ContactId = 1,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = DocumentStatus.Draft,
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        // Same customer database, different branch. The filter is what stops
        // this; RLS is behind it.
        Assert.NotEmpty(await mine.Quotes.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.Quotes.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task One_branch_cannot_read_another_branchs_sales_register()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        await using SalesDbContext mine = _postgres.CreateContext(customerId, Guid.NewGuid());
        await using SalesDbContext theirs = _postgres.CreateContext(customerId, Guid.NewGuid());

        mine.SalesRegister.Add(new SalesRegister
        {
            TransactionTypeCode = "INV",
            SourceId = Random.Shared.NextInt64(1, long.MaxValue),
            DocumentNo = "INV/00001",
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ContactId = 1,
            SupplyType = "B2B",
            HsnSacCode = "7113",
            GstRate = 18m,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        // This is the table GSTR-1 is filed from, and it was the one with
        // neither a query filter nor an RLS policy.
        Assert.NotEmpty(await mine.SalesRegister.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.SalesRegister.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task One_customer_cannot_read_another_customers_quotes()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        // Two different customers sharing the one test database — the case that
        // could never be tested before sal carried a real CustomerId column,
        // since two customers' data never sat in the same database at once.
        await using SalesDbContext mine = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        await using SalesDbContext theirs = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        mine.Quotes.Add(new Quote
        {
            TransactionTypeCode = "QTE",
            DocumentNo = $"QT/{Guid.NewGuid():N}"[..20],
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            ContactId = 1,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Status = DocumentStatus.Draft,
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.Quotes.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.Quotes.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task Row_level_security_covers_every_table_in_the_schema()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        // The second line of defence, asserted in the database because that is
        // the only place it exists — EF knows nothing about a policy. Fifteen of
        // sixteen carried one before this; the register did not.
        List<string> unprotected = [];
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText =
                "SELECT tablename FROM pg_tables WHERE schemaname = 'sal' AND NOT rowsecurity";

            await db.Database.OpenConnectionAsync(CancellationToken.None);
            await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);

            while (await reader.ReadAsync(CancellationToken.None))
            {
                unprotected.Add(reader.GetString(0));
            }
        }

        Assert.Empty(unprotected);
    }
}

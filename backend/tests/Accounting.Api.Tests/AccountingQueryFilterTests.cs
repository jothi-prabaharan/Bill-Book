using Microsoft.EntityFrameworkCore;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// Asserts the acc schema's tenant isolation the same way Sales.Api.Tests does:
/// over the whole model and the whole schema, backed by a real PostgreSQL so the
/// RLS half — which the model knows nothing about — is actually checked.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AccountingQueryFilterTests
{
    private readonly PostgresFixture _postgres;

    public AccountingQueryFilterTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Every_org_scoped_accounting_entity_has_a_query_filter()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using AccountingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> unfiltered = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters() is not { Count: > 0 })
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        Assert.Empty(unfiltered);
    }

    [SkippableFact]
    public async Task One_branch_cannot_read_another_branchs_payment_terms()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        await using AccountingDbContext mine = _postgres.CreateContext(customerId, Guid.NewGuid());
        await using AccountingDbContext theirs = _postgres.CreateContext(customerId, Guid.NewGuid());

        mine.PaymentTerms.Add(new PaymentTerm { TermName = "Branch-scoped term" });
        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.PaymentTerms.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.PaymentTerms.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task One_customer_cannot_read_another_customers_payment_terms()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using AccountingDbContext mine = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        await using AccountingDbContext theirs = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        mine.PaymentTerms.Add(new PaymentTerm { TermName = "Customer-scoped term" });
        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.PaymentTerms.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.PaymentTerms.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task Row_level_security_covers_every_table_in_the_schema()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using AccountingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        // NumberingSeries had no RLS policy at all before this migration, and the
        // four fixed-asset tables had a policy under a differently-named,
        // never-generalized "TenantPolicy" — this is the check that would have
        // caught both.
        List<string> unprotected = [];
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText =
                "SELECT tablename FROM pg_tables WHERE schemaname = 'acc' AND NOT rowsecurity";

            await db.Database.OpenConnectionAsync(CancellationToken.None);
            await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);

            while (await reader.ReadAsync(CancellationToken.None))
            {
                unprotected.Add(reader.GetString(0));
            }
        }

        Assert.Empty(unprotected);
    }

    /// <summary>
    /// Row-level security on acc is on, FORCEd, and has a policy.
    ///
    /// <b>All three, because one was being checked and the other two matter
    /// more.</b> The existing assertion read <c>pg_tables.rowsecurity</c>, which
    /// says RLS is switched on. It does not say a policy exists — a squashed
    /// migration that dropped one would leave the flag set — and it does not say
    /// <c>FORCE</c> is set. Without FORCE, RLS does not apply to the table's
    /// owner, and the application connects as the role that owns these tables:
    /// every policy in the product would be inert, leaving the EF query filter
    /// as the only guard, which is exactly the single point of failure having
    /// both is meant to avoid.
    /// </summary>
    [SkippableFact]
    public async Task Row_level_security_is_enabled_forced_and_policied_on_every_table()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using AccountingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            string.Empty,
            string.Join(
                "; ",
                await BillBook.Tests.Shared.RlsAudit.UnprotectedAsync(db, "acc")));
    }
}

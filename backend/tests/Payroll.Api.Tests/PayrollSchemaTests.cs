using Microsoft.EntityFrameworkCore;
using Payroll.Api.Services;
using Payroll.Entity.TableEntities;
using Payroll.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Payroll.Api.Tests;

/// <summary>What the pay schema guarantees, asked of a database built from the migration (TK-51).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class PayrollSchemaTests
{
    private readonly PostgresFixture _postgres;

    public PayrollSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Every_org_scoped_entity_has_a_query_filter_and_xmin()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PayrollDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var scoped = db.Model.GetEntityTypes().Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType)).ToList();

        Assert.Empty(scoped.Where(e => e.GetDeclaredQueryFilters() is not { Count: > 0 }).Select(e => e.ClrType.Name));
        Assert.Empty(scoped
            .Where(e => e.FindProperty(nameof(OrgScopedEntity.Version)) is not { } v || v.GetColumnName() != "xmin" || !v.IsConcurrencyToken)
            .Select(e => e.ClrType.Name));
    }

    [SkippableFact]
    public async Task Row_level_security_is_enabled_forced_and_policied_on_every_table()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PayrollDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            string.Empty,
            string.Join("; ", await BillBook.Tests.Shared.RlsAudit.UnprotectedAsync(db, "pay", "__EFMigrationsHistory")));
    }

    [SkippableFact]
    public async Task Seeding_a_branch_twice_adds_nothing_the_second_time()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using PayrollDbContext db = _postgres.CreateContext(customerId, orgId);

        Dictionary<string, int> first = await new PayrollSeeder(db).SeedForOrganizationAsync(orgId, default);
        Dictionary<string, int> second = await new PayrollSeeder(db).SeedForOrganizationAsync(orgId, default);

        Assert.All(first.Values, v => Assert.True(v >= 1));
        Assert.All(second.Values, v => Assert.Equal(0, v));
        Assert.Equal(1, await db.PayGroups.CountAsync());
    }
}

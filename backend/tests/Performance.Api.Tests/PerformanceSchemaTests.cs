using Microsoft.EntityFrameworkCore;
using Performance.Api.Services;
using Performance.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Performance.Api.Tests;

/// <summary>What the prf schema guarantees, asked of a database built from the migration (H11, TK-58).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class PerformanceSchemaTests
{
    private readonly PostgresFixture _postgres;

    public PerformanceSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public void Every_org_scoped_entity_has_a_query_filter_and_xmin()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        using PerformanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
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

        await using PerformanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            string.Empty,
            string.Join("; ", await BillBook.Tests.Shared.RlsAudit.UnprotectedAsync(db, "prf", "__EFMigrationsHistory")));
    }

    [SkippableFact]
    public async Task Seeding_a_branch_twice_adds_nothing_the_second_time()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using PerformanceDbContext db = _postgres.CreateContext(customerId, orgId);

        var seeder = new PerformanceSeeder(db);
        await seeder.SeedAsync(customerId, orgId, default);
        int initialScales = await db.RatingScales.CountAsync();
        int initialGroups = await db.CompetencyGroups.CountAsync();

        await seeder.SeedAsync(customerId, orgId, default);
        int secondScales = await db.RatingScales.CountAsync();
        int secondGroups = await db.CompetencyGroups.CountAsync();

        Assert.Equal(initialScales, secondScales);
        Assert.Equal(initialGroups, secondGroups);
        Assert.True(initialScales >= 1);
        Assert.True(initialGroups >= 1);
    }
}

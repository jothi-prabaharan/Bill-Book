using Microsoft.EntityFrameworkCore;
using Recruitment.Api.Services;
using Recruitment.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Recruitment.Api.Tests;

/// <summary>What the rec schema guarantees, asked of a database built from the migration (H10, TK-57).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class RecruitmentSchemaTests
{
    private readonly PostgresFixture _postgres;

    public RecruitmentSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public void Every_org_scoped_entity_has_a_query_filter_and_xmin()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        using RecruitmentDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
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

        await using RecruitmentDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            string.Empty,
            string.Join("; ", await BillBook.Tests.Shared.RlsAudit.UnprotectedAsync(db, "rec", "__EFMigrationsHistory")));
    }

    [SkippableFact]
    public async Task Seeding_a_branch_twice_adds_nothing_the_second_time()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using RecruitmentDbContext db = _postgres.CreateContext(customerId, orgId);

        var seeder = new RecruitmentSeeder(db);
        await seeder.SeedAsync(customerId, orgId, default);
        int initialCount = await db.NumberingSeries.CountAsync();

        await seeder.SeedAsync(customerId, orgId, default);
        int secondCount = await db.NumberingSeries.CountAsync();

        Assert.Equal(initialCount, secondCount);
        Assert.True(initialCount >= 1);
    }
}

using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using TimeLeave.Api.Services;
using TimeLeave.Repository;
using Xunit;

namespace TimeLeave.Api.Tests;

/// <summary>What the tla schema guarantees, asked of a database built from the migration (TK-49, TK-50).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class TimeLeaveSchemaTests
{
    private readonly PostgresFixture _postgres;

    public TimeLeaveSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Every_org_scoped_entity_has_a_query_filter_and_xmin()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using TimeLeaveDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
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

        await using TimeLeaveDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            string.Empty,
            string.Join("; ", await BillBook.Tests.Shared.RlsAudit.UnprotectedAsync(db, "tla", "__EFMigrationsHistory")));
    }

    [SkippableFact]
    public async Task Seeding_a_branch_twice_adds_nothing_the_second_time()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using TimeLeaveDbContext db = _postgres.CreateContext(customerId, orgId);

        var seeder = new TimeLeaveSeeder(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<TimeLeaveSeeder>.Instance);
        await seeder.SeedBranchAsync(orgId, default);
        int initialCount = await db.LeaveTypes.CountAsync();

        await seeder.SeedBranchAsync(orgId, default);
        int secondCount = await db.LeaveTypes.CountAsync();

        Assert.Equal(initialCount, secondCount);
        Assert.True(initialCount >= 5);
    }
}

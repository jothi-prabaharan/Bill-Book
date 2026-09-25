using Employee.Api.Services;
using Employee.Entity.TableEntities;
using Employee.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Employee.Api.Tests;

/// <summary>What the hrm schema guarantees, asked of a database built from the migration (TK-48).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class EmployeeSchemaTests
{
    private readonly PostgresFixture _postgres;

    public EmployeeSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Every_org_scoped_entity_has_a_query_filter_and_xmin()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using EmployeeDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var scoped = db.Model.GetEntityTypes().Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType)).ToList();

        Assert.Empty(scoped.Where(e => e.GetDeclaredQueryFilters() is not { Count: > 0 }).Select(e => e.ClrType.Name));
        Assert.Empty(scoped
            .Where(e => e.FindProperty(nameof(OrgScopedEntity.Version)) is not { } v || v.GetColumnName() != "xmin" || !v.IsConcurrencyToken)
            .Select(e => e.ClrType.Name));
    }

    /// <summary>Every hrm table is RLS-enabled, FORCEd and policied, from a clean migration.</summary>
    [SkippableFact]
    public async Task Row_level_security_is_enabled_forced_and_policied_on_every_table()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using EmployeeDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            string.Empty,
            string.Join("; ", await BillBook.Tests.Shared.RlsAudit.UnprotectedAsync(db, "hrm", "__EFMigrationsHistory")));
    }

    /// <summary>The sal bug of 21 August, asked of hrm before it can happen: no shadow foreign key.</summary>
    [SkippableFact]
    public async Task No_relationship_maps_a_shadow_foreign_key()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using EmployeeDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(db.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys().SelectMany(fk => fk.Properties).Where(p => p.IsShadowProperty())
                .Select(p => $"{e.ClrType.Name}.{p.Name}")));
    }

    [SkippableFact]
    public async Task Seeding_a_branch_twice_adds_nothing_the_second_time()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using EmployeeDbContext db = _postgres.CreateContext(customerId, orgId);

        Dictionary<string, int> first = await new EmployeeSeeder(db).SeedForOrganizationAsync(orgId, default);
        Dictionary<string, int> second = await new EmployeeSeeder(db).SeedForOrganizationAsync(orgId, default);

        Assert.All(first.Values, v => Assert.Equal(1, v));
        Assert.All(second.Values, v => Assert.Equal(0, v));
        Assert.Equal(1, await db.Departments.CountAsync());
        Assert.True(await db.NumberingSeries.AnyAsync(n => n.SeriesCode == "EMP"));
    }

    [SkippableFact]
    public async Task One_branch_never_sees_another_branchs_departments()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), mine = Guid.NewGuid();
        await using (EmployeeDbContext db = _postgres.CreateContext(customerId, mine))
        {
            db.Departments.Add(new Department { Code = "MINE", Name = "Mine" });
            await db.SaveChangesAsync();
        }

        await using EmployeeDbContext other = _postgres.CreateContext(customerId, Guid.NewGuid());
        Assert.False(await other.Departments.AnyAsync(d => d.Code == "MINE"));
    }
}

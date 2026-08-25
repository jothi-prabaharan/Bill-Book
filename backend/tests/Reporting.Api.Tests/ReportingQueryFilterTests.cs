using Microsoft.EntityFrameworkCore;
using Reporting.Entity.TableEntities;
using Reporting.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// Asserts the rpt schema's tenant isolation the same way Sales.Api.Tests does.
/// Only Reports/ReportViews/ReportDetails are covered — ReportMasters and
/// ReportColumns are the report catalog, plain (not OrgScopedEntity) global
/// reference data shared by every customer, so there is nothing to filter on
/// them.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ReportingQueryFilterTests
{
    private readonly PostgresFixture _postgres;

    public ReportingQueryFilterTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Every_org_scoped_reporting_entity_has_a_query_filter()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ReportingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> unfiltered = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters() is not { Count: > 0 })
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        Assert.Empty(unfiltered);
    }

    [SkippableFact]
    public async Task One_customer_cannot_read_another_customers_saved_reports()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ReportingDbContext mine = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        await using ReportingDbContext theirs = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        mine.Reports.Add(new Report
        {
            ReportKey = $"rpt-{Guid.NewGuid():N}"[..20],
            Title = "Customer-scoped report",
            RequiredPermission = "reporting.view",
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.Reports.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.Reports.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task Row_level_security_covers_every_org_scoped_table_in_the_schema()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ReportingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> unprotected = [];
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText =
                "SELECT tablename FROM pg_tables WHERE schemaname = 'rpt' "
                + "AND tablename IN ('Reports', 'ReportViews', 'ReportDetails') AND NOT rowsecurity";

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

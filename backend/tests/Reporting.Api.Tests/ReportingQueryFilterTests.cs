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

    /// <summary>
    /// Row-level security on <c>rpt</c> is on, FORCEd, and has a policy — on
    /// every table that has a tenant to scope.
    ///
    /// <b>Two tables have none, and they are named here rather than skipped by
    /// a list.</b> The previous assertion checked exactly three tables by name,
    /// which is a list with exceptions on it — the shape that has hidden every
    /// gap this project has found, because a table added later is simply not on
    /// it. <c>ReportMasters</c> and <c>ReportColumns</c> hold the imported
    /// <c>reports.json</c> specification: global reference data with no
    /// <c>CustomerId</c> or <c>OrgId</c> column at all, so there is nothing
    /// per-customer in them to leak and no column a policy could filter on.
    ///
    /// The other three are per-branch saved layouts and take the full treatment.
    /// FORCE is the part nothing was checking anywhere: without it RLS does not
    /// apply to the owner, which is the role the application connects as.
    /// </summary>
    [SkippableFact]
    public async Task Row_level_security_is_enabled_forced_and_policied_on_every_tenant_table()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ReportingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            string.Empty,
            string.Join(
                "; ",
                await BillBook.Tests.Shared.RlsAudit.UnprotectedAsync(
                    db, "rpt", "ReportMasters", "ReportColumns")));
    }
}

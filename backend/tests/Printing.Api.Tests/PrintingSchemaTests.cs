using Microsoft.EntityFrameworkCore;
using Printing.Api.Rendering;
using Printing.Entity.Enums;
using Printing.Entity.TableEntities;
using Printing.Repository;
using Printing.Repository.SeedData;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>
/// What the prt schema guarantees, asked of a real PostgreSQL built from the
/// migration rather than from the model.
///
/// <b>The schema was written and is therefore unverified</b> — which is the
/// failure this repository has hit twice, in sal and in con. A schema nobody
/// queries is a schema nobody has checked, and "the migration applies" says
/// nothing about the relationship EF inferred or the policy the database is
/// actually enforcing.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PrintingSchemaTests
{
    private readonly PostgresFixture _postgres;

    public PrintingSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Every_org_scoped_printing_entity_has_a_query_filter()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> unfiltered = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters() is not { Count: > 0 })
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        Assert.Empty(unfiltered);
    }

    [SkippableFact]
    public async Task Every_org_scoped_printing_entity_maps_xmin_as_its_concurrency_token()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> wrong = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.FindProperty(nameof(OrgScopedEntity.Version)) is not { } version
                || version.GetColumnName() != "xmin"
                || !version.IsConcurrencyToken)
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        Assert.Empty(wrong);
    }

    /// <summary>
    /// Row-level security on prt is on, FORCEd, and has a policy.
    ///
    /// <b>All three, because the flag alone is the one that was being
    /// checked.</b> <c>pg_tables.rowsecurity</c> says RLS is switched on. It
    /// does not say a policy exists, and it does not say FORCE is set — and
    /// without FORCE, RLS does not apply to the table's owner, which is the role
    /// the application connects as. Every policy here would be inert and the EF
    /// query filter would be the only guard left, which is exactly the single
    /// point of failure that having both is meant to avoid.
    ///
    /// No exemptions: every table in prt carries CustomerId and OrgId. The
    /// migrations-history table is not in this schema under the fixture's
    /// options, and it holds no tenant data in any case.
    /// </summary>
    [SkippableFact]
    public async Task Row_level_security_is_enabled_forced_and_policied_on_every_table()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            string.Empty,
            string.Join(
                "; ",
                await BillBook.Tests.Shared.RlsAudit.UnprotectedAsync(db, "prt")));
    }

    /// <summary>
    /// No foreign key carries a shadow property, and no column is named after
    /// one.
    ///
    /// <b>This is the sal bug, asked of prt before it can happen.</b> A
    /// header-to-line relationship configured without naming its navigation left
    /// EF to map the collection a second time by convention, with shadow keys
    /// called QuoteId1 and nine others — so every insert filled the shadow
    /// column and left the real NOT NULL one at zero. prt has no header/line
    /// pair today; this asserts it still has none after the next one is added.
    /// </summary>
    [SkippableFact]
    public async Task No_relationship_maps_a_shadow_foreign_key()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> shadowed = [.. db.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()
                .SelectMany(fk => fk.Properties)
                .Where(p => p.IsShadowProperty())
                .Select(p => $"{e.ClrType.Name}.{p.Name}"))
            .OrderBy(name => name)];

        Assert.Empty(shadowed);
    }

    /// <summary>
    /// A template written by one branch is invisible to another, and the jsonb
    /// columns survive the round trip.
    ///
    /// <b>The round trip is the half a model assertion cannot make.</b> Settings
    /// and Content go through a value converter; without the matching
    /// ValueComparer EF compares them by reference, so an edit in place produces
    /// no UPDATE and the save silently does nothing.
    /// </summary>
    [SkippableFact]
    public async Task One_branch_cannot_read_another_branchs_templates()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var myOrgId = Guid.NewGuid();
        var theirOrgId = Guid.NewGuid();

        await using PrintingDbContext mine = _postgres.CreateContext(customerId, myOrgId);
        await using PrintingDbContext theirs = _postgres.CreateContext(customerId, theirOrgId);

        var template = new PrintTemplate
        {
            DocumentTypeCode = "INV",
            TemplateName = "Tax invoice",
            IsDefault = true,
            SeedVersion = DefaultLayoutGenerator.SeedVersion,
        };

        PrintSegments.SetHtml(template.Content, PrintSegment.Details, "<p>{{Invoice.Number}}</p>");

        mine.PrintTemplates.Add(template);
        await mine.SaveChangesAsync(CancellationToken.None);

        PrintTemplate saved = Assert.Single(await mine.PrintTemplates.ToListAsync(CancellationToken.None));
        Assert.Equal(
            "<p>{{Invoice.Number}}</p>",
            PrintSegments.Html(saved.Content, PrintSegment.Details));

        Assert.Empty(await theirs.PrintTemplates.ToListAsync(CancellationToken.None));
    }
}

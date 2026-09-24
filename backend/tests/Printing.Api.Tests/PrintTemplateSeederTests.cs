using Printing.Api.Services;
using Printing.Entity.Models;
using Printing.Repository;
using Printing.Repository.SeedData;
using Shared.Kernel.Printing;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>
/// A branch's starting templates, now that Printing seeds them rather than
/// Master (TK-24). Existing branches are re-seeded through the same path rather
/// than copied, so idempotence is the property everything rests on.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PrintTemplateSeederTests
{
    private readonly PostgresFixture _postgres;

    public PrintTemplateSeederTests(PostgresFixture postgres) => _postgres = postgres;

    private static CancellationToken Ct => CancellationToken.None;

    [SkippableFact]
    public async Task Seeding_a_branch_twice_adds_nothing_the_second_time()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        await using PrintingDbContext db = _postgres.CreateContext(customer, org);
        var seeder = new PrintTemplateSeeder(db);

        int first = await seeder.SeedForOrganizationAsync(org, Ct);
        int second = await seeder.SeedForOrganizationAsync(org, Ct);

        Assert.Equal(DocumentTypeCatalog.All.Count, first);
        Assert.Equal(0, second);

        IReadOnlyList<PrintTemplateListItem> list =
            await new PrintTemplateService(db, new PrintRenderer()).ListAsync(null, Ct);

        Assert.Equal(DocumentTypeCatalog.All.Count, list.Count);
        Assert.All(list, t => Assert.True(t.IsDefault));
    }

    [SkippableFact]
    public async Task A_seeded_branch_renders_from_its_own_default_rather_than_the_platform_layout()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid org = Guid.NewGuid();
        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), org);

        await new PrintTemplateSeeder(db).SeedForOrganizationAsync(org, Ct);

        PrintTemplateResult<RenderPrintResponse> result =
            await new PrintTemplateService(db, new PrintRenderer()).RenderAsync(new RenderPrintRequest
            {
                DocumentTypeCode = "INV",
                Payload = new RenderPayload(),
            }, Ct);

        Assert.Equal(PrintTemplateSource.BranchDefault, result.Value!.Source);
        Assert.NotNull(result.Value.PrintTemplateId);
    }
}

/// <summary>The seed builder, with no database.</summary>
public sealed class PrintTemplateSeedTests
{
    [Fact]
    public void A_new_branch_gets_one_default_per_printable_document_type()
    {
        Guid org = Guid.NewGuid();

        var rows = PrintTemplateSeed.Build(org, []);

        Assert.Equal(
            DocumentTypeCatalog.All.Select(p => p.Code).Order(),
            rows.Select(r => r.DocumentTypeCode).Order());
        Assert.All(rows, r =>
        {
            Assert.Equal(org, r.OrgId);
            Assert.True(r.IsDefault);
            Assert.True(r.IsActive);
            Assert.Equal(DefaultLayoutGenerator.SeedVersion, r.SeedVersion);
        });
    }

    [Fact]
    public void A_document_type_the_branch_already_has_is_left_alone()
    {
        // Trimmed and case-insensitive: the column is fixed-length char(3), and a
        // padded or lower-case code must still count as present.
        var rows = PrintTemplateSeed.Build(Guid.NewGuid(), ["INV", "qte", "SOR "]);

        Assert.DoesNotContain(rows, r => r.DocumentTypeCode is "INV" or "QTE" or "SOR");
        Assert.Equal(DocumentTypeCatalog.All.Count - 3, rows.Count);
    }
}

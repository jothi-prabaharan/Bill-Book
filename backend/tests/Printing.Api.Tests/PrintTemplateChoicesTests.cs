using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Printing.Api.Controllers;
using Printing.Entity.Models;
using Printing.Entity.TableEntities;
using Printing.Repository;
using Printing.Repository.Repositories;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>
/// The Print template field on a create page (TK-192): the active layouts for
/// one document type, the branch default first, read through the document's
/// own module rather than the template editor's Settings permission.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PrintTemplateChoicesTests
{
    private readonly PostgresFixture _postgres;

    public PrintTemplateChoicesTests(PostgresFixture postgres) => _postgres = postgres;

    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>Another layout for the seeded template's document type.</summary>
    private static PrintTemplate CopyOf(PrintingDbContext db, PrintTemplate seeded, string name, bool active)
    {
        var copy = new PrintTemplate();
        db.Entry(copy).CurrentValues.SetValues(db.Entry(seeded).CurrentValues);
        copy.PrintTemplateId = 0;
        copy.TemplateName = name;
        copy.IsDefault = false;
        copy.IsActive = active;
        return copy;
    }

    [SkippableFact]
    public async Task A_document_type_offers_its_active_layouts_with_the_default_first()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid org = Guid.NewGuid();
        await using PrintingDbContext db = _postgres.CreateContext(Guid.NewGuid(), org);
        await new PrintTemplateSeederRepository(db).SeedForOrganizationAsync(org, Ct);

        PrintTemplate seeded = await db.PrintTemplates.SingleAsync(t => t.DocumentTypeCode == "INV");
        // Named to sort ahead of the default, so the order is the default's doing.
        db.PrintTemplates.Add(CopyOf(db, seeded, "A4 letterhead", active: true));
        db.PrintTemplates.Add(CopyOf(db, seeded, "Old layout", active: false));
        await db.SaveChangesAsync();

        IReadOnlyList<PrintTemplateChoice> choices =
            await new PrintTemplateRepository(db, new PrintRendererRepository()).ChoicesAsync(" inv ", Ct);

        Assert.Equal(2, choices.Count);
        Assert.True(choices[0].IsDefault);
        Assert.Equal(seeded.PrintTemplateId, choices[0].PrintTemplateId);
        Assert.Equal("A4 letterhead", choices[1].TemplateName);
        Assert.DoesNotContain(choices, c => c.TemplateName == "Old layout");
    }

    [Fact]
    public async Task A_module_offers_layouts_for_its_own_document_types_and_no_other()
    {
        // A refusal is decided before the repository is asked anything.
        var templates = new PrintTemplateRepository(null!, new PrintRendererRepository());

        Assert.IsType<NotFoundResult>(await new SalesPrintChoicesController(templates).Templates("BIL", Ct));
        Assert.IsType<NotFoundResult>(await new PurchasePrintChoicesController(templates).Templates("INV", Ct));
        Assert.IsType<NotFoundResult>(await new BankingPrintChoicesController(templates).Templates("JRN", Ct));
        Assert.IsType<NotFoundResult>(await new AccountingPrintChoicesController(templates).Templates("RCM", Ct));
    }
}

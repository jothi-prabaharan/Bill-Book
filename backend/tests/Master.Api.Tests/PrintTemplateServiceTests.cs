using Master.Api.Services;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Printing;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Acceptance tests 1, 2, 3, 9, 10 and 12 — the ones that are only true of a
/// real database. The default rule is a filtered unique index, the stale check
/// has to leave a row untouched, and the tenancy boundary is a query filter;
/// none of those can be shown against an in-memory provider.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PrintTemplateServiceTests
{
    private readonly PostgresFixture _postgres;

    public PrintTemplateServiceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>Nothing here cancels; naming it keeps the calls readable.</summary>
    private static CancellationToken Ct => CancellationToken.None;

    private PrintTemplateService Service(ContactsDbContext db) => new(db, new PrintRenderer());

    private static UpdatePrintTemplateRequest Update(PrintTemplateDetail detail, Action<UpdatePrintTemplateRequest> edit)
    {
        var request = new UpdatePrintTemplateRequest
        {
            TemplateName = detail.TemplateName,
            Settings = detail.Settings,
            Content = detail.Content,
            TemplateVersion = detail.TemplateVersion,
        };

        edit(request);
        return request;
    }

    private static CreatePrintTemplateRequest Create(string name, string code = "INV") =>
        new() { DocumentTypeCode = code, TemplateName = name };

    // ---- 1. No bleed between two templates of one document type ----------

    [SkippableFact]
    public async Task Two_templates_of_one_document_type_keep_their_own_settings_and_content()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        await using ContactsDbContext db = _postgres.CreateContext(customer, org);
        PrintTemplateService service = Service(db);

        PrintTemplateDetail first = (await service.CreateAsync(Create("A4 layout"), Ct)).Value!;
        PrintTemplateDetail second = (await service.CreateAsync(Create("A5 layout"), Ct)).Value!;

        await service.UpdateAsync(second.PrintTemplateId, Update(second, r =>
        {
            r.Settings.PaperSize = PaperSize.A5;
            r.Settings.MarginTopMm = 4;
            r.Settings.SegMargins.Details.AboveMm = 9;
            r.Content.HeaderHtml = "<div>Second only</div>";
        }), Ct);

        await using ContactsDbContext fresh = _postgres.CreateContext(customer, org);
        PrintTemplateService reader = Service(fresh);

        PrintTemplateDetail a = (await reader.GetAsync(first.PrintTemplateId, Ct)).Value!;
        PrintTemplateDetail b = (await reader.GetAsync(second.PrintTemplateId, Ct)).Value!;

        Assert.Equal(PaperSize.A4, a.Settings.PaperSize);
        Assert.Equal(16d, a.Settings.MarginTopMm);
        Assert.Equal(0d, a.Settings.SegMargins.Details.AboveMm);
        Assert.DoesNotContain("Second only", a.Content.HeaderHtml, StringComparison.Ordinal);

        Assert.Equal(PaperSize.A5, b.Settings.PaperSize);
        Assert.Equal(4d, b.Settings.MarginTopMm);
        Assert.Equal(9d, b.Settings.SegMargins.Details.AboveMm);
        Assert.Contains("Second only", b.Content.HeaderHtml, StringComparison.Ordinal);
    }

    // ---- 2. Exactly one default, under concurrency -----------------------

    [SkippableFact]
    public async Task Setting_a_default_clears_the_previous_one()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        await using ContactsDbContext db = _postgres.CreateContext(customer, org);
        PrintTemplateService service = Service(db);

        PrintTemplateDetail first = (await service.CreateAsync(Create("First"), Ct)).Value!;
        PrintTemplateDetail second = (await service.CreateAsync(Create("Second"), Ct)).Value!;

        Assert.True(first.IsDefault);
        Assert.False(second.IsDefault);

        Assert.Equal(
            PrintTemplateOutcome.Ok,
            await service.SetDefaultAsync(second.PrintTemplateId, Ct));

        Assert.Equal(1, await CountDefaults(customer, org));
        Assert.True((await service.GetAsync(second.PrintTemplateId, Ct)).Value!.IsDefault);
        Assert.False((await service.GetAsync(first.PrintTemplateId, Ct)).Value!.IsDefault);
    }

    [SkippableFact]
    public async Task Concurrent_requests_to_set_a_default_leave_exactly_one()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        long firstId;
        long secondId;

        await using (ContactsDbContext seed = _postgres.CreateContext(customer, org))
        {
            PrintTemplateService service = Service(seed);
            firstId = (await service.CreateAsync(Create("First"), Ct)).Value!.PrintTemplateId;
            secondId = (await service.CreateAsync(Create("Second"), Ct)).Value!.PrintTemplateId;
        }

        // Two separate contexts, so these are two real database transactions
        // racing — not two calls serialised by a lock inside one process, which
        // would prove nothing about a second replica.
        async Task Race(long id)
        {
            await using ContactsDbContext db = _postgres.CreateContext(customer, org);
            try
            {
                await Service(db).SetDefaultAsync(id, Ct);
            }
            catch (DbUpdateException)
            {
                // One of them losing is a correct outcome; both winning is not.
            }
            catch (Npgsql.PostgresException)
            {
                // Likewise a unique-violation raised by the filtered index.
            }
        }

        await Task.WhenAll(Race(firstId), Race(secondId));

        Assert.Equal(1, await CountDefaults(customer, org));
    }

    // ---- 3. Stale saves ---------------------------------------------------

    [SkippableFact]
    public async Task A_save_with_a_stale_version_is_refused_and_writes_nothing()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        await using ContactsDbContext db = _postgres.CreateContext(customer, org);
        PrintTemplateService service = Service(db);

        PrintTemplateDetail created = (await service.CreateAsync(Create("Shared"), Ct)).Value!;

        // The first editor saves.
        await service.UpdateAsync(created.PrintTemplateId, Update(created, r =>
        {
            r.TemplateName = "Saved by the first editor";
            r.Content.HeaderHtml = "<div>First editor</div>";
        }), Ct);

        // The second editor, still holding the version it opened.
        PrintTemplateResult<PrintTemplateDetail> stale = await service.UpdateAsync(
            created.PrintTemplateId,
            Update(created, r =>
            {
                r.TemplateName = "Saved by the second editor";
                r.Content.HeaderHtml = "<div>Second editor</div>";
                r.TemplateVersion = created.TemplateVersion;
            }),
            Ct);

        Assert.Equal(PrintTemplateOutcome.Stale, stale.Outcome);

        await using ContactsDbContext fresh = _postgres.CreateContext(customer, org);
        PrintTemplateDetail stored = (await Service(fresh)
            .GetAsync(created.PrintTemplateId, Ct)).Value!;

        // Refused means untouched, not merged.
        Assert.Equal("Saved by the first editor", stored.TemplateName);
        Assert.Contains("First editor", stored.Content.HeaderHtml, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Every_save_moves_the_version_on()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        await using ContactsDbContext db = _postgres.CreateContext(customer, org);
        PrintTemplateService service = Service(db);

        PrintTemplateDetail created = (await service.CreateAsync(Create("Versioned"), Ct)).Value!;
        Assert.Equal(1, created.TemplateVersion);

        PrintTemplateDetail saved = (await service.UpdateAsync(
            created.PrintTemplateId,
            Update(created, _ => { }),
            Ct)).Value!;

        Assert.Equal(2, saved.TemplateVersion);
    }

    // ---- 9. A deleted template leaves its documents printable -------------

    [SkippableFact]
    public async Task A_document_naming_a_deleted_template_still_prints_via_the_default()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        await using ContactsDbContext db = _postgres.CreateContext(customer, org);
        PrintTemplateService service = Service(db);

        PrintTemplateDetail standard = (await service.CreateAsync(Create("Standard"), Ct)).Value!;
        PrintTemplateDetail seasonal = (await service.CreateAsync(Create("Seasonal"), Ct)).Value!;

        // A document was raised naming the seasonal template.
        long named = seasonal.PrintTemplateId;
        Assert.Equal(
            PrintTemplateSource.Explicit,
            (await service.ResolveAsync("INV", named, Ct)).Source);

        Assert.Equal(
            PrintTemplateOutcome.Ok,
            await service.DeleteAsync(named, Ct));

        PrintTemplateResolution resolved = await service.ResolveAsync("INV", named, Ct);

        // The document is still printable; it just falls through to the default.
        Assert.Equal(PrintTemplateSource.BranchDefault, resolved.Source);
        Assert.Equal(standard.PrintTemplateId, resolved.PrintTemplateId);
        Assert.NotEqual(string.Empty, resolved.Content.DetailsHtml);
    }

    [SkippableFact]
    public async Task A_branch_with_no_stored_template_still_prints_from_the_platform_seed()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        PrintTemplateResolution resolved = await Service(db)
            .ResolveAsync("INV", null, Ct);

        Assert.Equal(PrintTemplateSource.PlatformSeed, resolved.Source);
        Assert.Null(resolved.PrintTemplateId);
        Assert.NotEqual(string.Empty, resolved.Content.DetailsHtml);
    }

    [SkippableFact]
    public async Task The_default_and_the_last_template_cannot_be_deleted()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        PrintTemplateService service = Service(db);

        PrintTemplateDetail only = (await service.CreateAsync(Create("Only one"), Ct)).Value!;

        Assert.Equal(
            PrintTemplateOutcome.LastTemplate,
            await service.DeleteAsync(only.PrintTemplateId, Ct));
    }

    // ---- 10. A rename is visible immediately ------------------------------

    [SkippableFact]
    public async Task A_rename_shows_in_the_list_the_picker_reads()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        await using ContactsDbContext db = _postgres.CreateContext(customer, org);
        PrintTemplateService service = Service(db);

        PrintTemplateDetail created = (await service.CreateAsync(Create("Old name"), Ct)).Value!;

        await service.UpdateAsync(
            created.PrintTemplateId,
            Update(created, r => r.TemplateName = "New name"),
            Ct);

        await using ContactsDbContext fresh = _postgres.CreateContext(customer, org);
        IReadOnlyList<PrintTemplateListItem> list = await Service(fresh)
            .ListAsync("INV", Ct);

        Assert.Contains(list, t => t.TemplateName == "New name");
        Assert.DoesNotContain(list, t => t.TemplateName == "Old name");
    }

    [SkippableFact]
    public async Task Two_templates_of_one_type_cannot_share_a_name()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        PrintTemplateService service = Service(db);

        await service.CreateAsync(Create("Duplicate"), Ct);
        PrintTemplateResult<PrintTemplateDetail> second =
            await service.CreateAsync(Create("Duplicate"), Ct);

        Assert.Equal(PrintTemplateOutcome.NameTaken, second.Outcome);
    }

    [SkippableFact]
    public async Task A_duplicate_is_a_deep_copy_that_does_not_reach_back()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        await using ContactsDbContext db = _postgres.CreateContext(customer, org);
        PrintTemplateService service = Service(db);

        PrintTemplateDetail source = (await service.CreateAsync(Create("Source"), Ct)).Value!;

        PrintTemplateDetail copy = (await service.CreateAsync(
            new CreatePrintTemplateRequest
            {
                DocumentTypeCode = "INV",
                TemplateName = "Copy",
                CopyFromId = source.PrintTemplateId,
            },
            Ct)).Value!;

        await service.UpdateAsync(copy.PrintTemplateId, Update(copy, r =>
        {
            r.Content.HeaderHtml = "<div>Changed on the copy</div>";
            r.Settings.MarginLeftMm = 33;
        }), Ct);

        await using ContactsDbContext fresh = _postgres.CreateContext(customer, org);
        PrintTemplateDetail original = (await Service(fresh)
            .GetAsync(source.PrintTemplateId, Ct)).Value!;

        Assert.DoesNotContain("Changed on the copy", original.Content.HeaderHtml, StringComparison.Ordinal);
        Assert.Equal(18d, original.Settings.MarginLeftMm);
    }

    // ---- 12. Tenancy ------------------------------------------------------

    [SkippableFact]
    public async Task One_branch_cannot_see_another_branchs_template()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid orgA = Guid.NewGuid();
        Guid orgB = Guid.NewGuid();

        long id;
        await using (ContactsDbContext a = _postgres.CreateContext(customer, orgA))
        {
            id = (await Service(a).CreateAsync(Create("Branch A only"), Ct))
                .Value!.PrintTemplateId;
        }

        await using ContactsDbContext b = _postgres.CreateContext(customer, orgB);
        PrintTemplateService service = Service(b);

        // Not found, never forbidden: a 403 would confirm the row exists, which
        // is itself a leak across the boundary.
        Assert.Equal(
            PrintTemplateOutcome.NotFound,
            (await service.GetAsync(id, Ct)).Outcome);

        Assert.DoesNotContain(
            await service.ListAsync("INV", Ct),
            t => t.PrintTemplateId == id);
    }

    [SkippableFact]
    public async Task One_customer_cannot_see_another_customers_template()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgA = Guid.NewGuid();
        Guid orgB = Guid.NewGuid();

        long id;
        await using (ContactsDbContext a = _postgres.CreateContext(Guid.NewGuid(), orgA))
        {
            id = (await Service(a).CreateAsync(Create("Customer A only"), Ct))
                .Value!.PrintTemplateId;
        }

        await using ContactsDbContext b = _postgres.CreateContext(Guid.NewGuid(), orgB);

        Assert.Equal(
            PrintTemplateOutcome.NotFound,
            (await Service(b).GetAsync(id, Ct)).Outcome);
    }

    // ---- Refusals ---------------------------------------------------------

    [SkippableFact]
    public async Task Markup_that_cannot_be_printed_is_refused_naming_what_was_wrong()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        PrintTemplateService service = Service(db);

        PrintTemplateDetail created = (await service.CreateAsync(Create("Guarded"), Ct)).Value!;

        PrintTemplateResult<PrintTemplateDetail> refused = await service.UpdateAsync(
            created.PrintTemplateId,
            Update(created, r => r.Content.HeaderHtml = "<div onclick=\"steal()\">x</div><script>alert(1)</script>"),
            Ct);

        Assert.Equal(PrintTemplateOutcome.InvalidSegmentHtml, refused.Outcome);
        Assert.Contains(refused.Details, d => d.Contains("onclick", StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public async Task Page_settings_that_leave_nothing_to_print_in_are_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        PrintTemplateService service = Service(db);

        PrintTemplateDetail created = (await service.CreateAsync(Create("Squeezed"), Ct)).Value!;

        PrintTemplateResult<PrintTemplateDetail> refused = await service.UpdateAsync(
            created.PrintTemplateId,
            Update(created, r =>
            {
                r.Settings.MarginLeftMm = 120;
                r.Settings.MarginRightMm = 120;
            }),
            Ct);

        Assert.Equal(PrintTemplateOutcome.InvalidGeometry, refused.Outcome);
    }

    [SkippableFact]
    public async Task A_pinned_footer_is_stored_with_the_fixed_footer_pinned_too()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        await using ContactsDbContext db = _postgres.CreateContext(customer, org);
        PrintTemplateService service = Service(db);

        PrintTemplateDetail created = (await service.CreateAsync(Create("Pinned"), Ct)).Value!;

        await service.UpdateAsync(created.PrintTemplateId, Update(created, r =>
        {
            r.Settings.FooterPos = SegmentPosition.Bottom;
            r.Settings.FixedFooterPref = SegmentPosition.Inline;
        }), Ct);

        await using ContactsDbContext fresh = _postgres.CreateContext(customer, org);
        PrintTemplateDetail stored = (await Service(fresh)
            .GetAsync(created.PrintTemplateId, Ct)).Value!;

        // Coerced on the way in, and the preference kept for when it is unpinned.
        Assert.Equal(SegmentPosition.Bottom, stored.Settings.FixedFooterPos);
        Assert.Equal(SegmentPosition.Inline, stored.Settings.FixedFooterPref);
    }

    [SkippableFact]
    public async Task Seeding_a_branch_twice_adds_nothing_the_second_time()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();

        await using ContactsDbContext db = _postgres.CreateContext(customer, org);
        var seeder = new PrintTemplateSeeder(db);

        int first = await seeder.SeedForOrganizationAsync(org, Ct);
        int second = await seeder.SeedForOrganizationAsync(org, Ct);

        Assert.Equal(DocumentTypeCatalog.All.Count, first);
        Assert.Equal(0, second);

        IReadOnlyList<PrintTemplateListItem> list = await Service(db)
            .ListAsync(null, Ct);

        Assert.Equal(DocumentTypeCatalog.All.Count, list.Count);
        Assert.All(list, t => Assert.True(t.IsDefault));
    }

    [SkippableFact]
    public async Task A_preview_renders_against_sample_data_without_a_document()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        PrintTemplateService service = Service(db);

        PrintTemplateDetail created = (await service.CreateAsync(Create("Previewed"), Ct)).Value!;

        PrintTemplateResult<PrintPreviewResponse> preview = await service.PreviewAsync(
            created.PrintTemplateId, new PreviewPrintTemplateRequest(), Ct);

        Assert.Equal(PrintTemplateOutcome.Ok, preview.Outcome);
        Assert.True(preview.Value!.PageCount >= 1);
        Assert.Contains("Sample Traders", preview.Value.Html, StringComparison.Ordinal);
        Assert.Empty(preview.Value.UnknownTags);
    }

    private async Task<int> CountDefaults(Guid customer, Guid org)
    {
        await using ContactsDbContext db = _postgres.CreateContext(customer, org);
        return await db.PrintTemplates.CountAsync(t => t.IsDefault && t.IsActive, Ct);
    }
}

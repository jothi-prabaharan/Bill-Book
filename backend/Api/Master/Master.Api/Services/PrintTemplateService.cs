using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Printing;

namespace Master.Api.Services;

/// <summary>
/// The print template master. Branch-scoped throughout — the query filter on
/// ContactsDbContext supplies CustomerId and OrgId, so no method here repeats
/// them and none can forget to.
/// </summary>
public sealed class PrintTemplateService
{
    private readonly ContactsDbContext _db;
    private readonly PrintRenderer _renderer;

    public PrintTemplateService(ContactsDbContext db, PrintRenderer renderer)
    {
        _db = db;
        _renderer = renderer;
    }

    public async Task<IReadOnlyList<PrintTemplateListItem>> ListAsync(string? documentTypeCode, CancellationToken ct)
    {
        IQueryable<PrintTemplate> query = _db.PrintTemplates.Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(documentTypeCode))
        {
            string code = documentTypeCode.Trim().ToUpperInvariant();
            query = query.Where(t => t.DocumentTypeCode == code);
        }

        return await query
            .OrderBy(t => t.DocumentTypeCode)
            .ThenByDescending(t => t.IsDefault)
            .ThenBy(t => t.TemplateName)
            .Select(t => new PrintTemplateListItem
            {
                PrintTemplateId = t.PrintTemplateId,
                TemplateName = t.TemplateName,
                DocumentTypeCode = t.DocumentTypeCode,
                IsDefault = t.IsDefault,
                UpdatedAt = t.ModifiedAt ?? t.CreatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<PrintTemplateResult<PrintTemplateDetail>> GetAsync(long id, CancellationToken ct)
    {
        PrintTemplate? template = await Find(id, ct);

        return template is null
            ? PrintTemplateResult<PrintTemplateDetail>.Fail(PrintTemplateOutcome.NotFound)
            : PrintTemplateResult<PrintTemplateDetail>.Ok(Detail(template));
    }

    public async Task<PrintTemplateResult<PrintTemplateDetail>> CreateAsync(
        CreatePrintTemplateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        DocumentTypeProfile? profile = DocumentTypeCatalog.Find(request.DocumentTypeCode);
        if (profile is null)
        {
            return PrintTemplateResult<PrintTemplateDetail>.Fail(PrintTemplateOutcome.InvalidDocumentType);
        }

        string name = request.TemplateName.Trim();
        if (await NameTakenAsync(profile.Code, name, null, ct))
        {
            return PrintTemplateResult<PrintTemplateDetail>.Fail(PrintTemplateOutcome.NameTaken);
        }

        PrintSettings settings;
        PrintContent content;
        int seedVersion;

        if (request.CopyFromId is long sourceId)
        {
            PrintTemplate? source = await Find(sourceId, ct);
            if (source is null)
            {
                return PrintTemplateResult<PrintTemplateDetail>.Fail(PrintTemplateOutcome.NotFound);
            }

            // A deep copy, not a shared reference: editing the duplicate must
            // not reach back into the template it came from.
            settings = PrintJson.Deserialize<PrintSettings>(PrintJson.Serialize(source.Settings));
            content = PrintJson.Deserialize<PrintContent>(PrintJson.Serialize(source.Content));
            seedVersion = source.SeedVersion;
        }
        else
        {
            settings = request.Settings ?? DefaultLayoutGenerator.BuildSettings();
            content = request.Content ?? DefaultLayoutGenerator.Build(profile);
            seedVersion = request.Content is null ? DefaultLayoutGenerator.SeedVersion : 0;
        }

        PrintTemplateResult<PrintTemplateDetail>? refusal = Prepare(settings, content);
        if (refusal is not null)
        {
            return refusal;
        }

        // The first template for a document type is its default, or the branch
        // would have a document type nothing prints.
        bool hasDefault = await _db.PrintTemplates
            .AnyAsync(t => t.DocumentTypeCode == profile.Code && t.IsDefault && t.IsActive, ct);

        var template = new PrintTemplate
        {
            DocumentTypeCode = profile.Code,
            TemplateName = name,
            IsDefault = !hasDefault,
            IsActive = true,
            Settings = settings,
            Content = content,
            TemplateVersion = 1,
            SeedVersion = seedVersion,
        };

        _db.PrintTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return PrintTemplateResult<PrintTemplateDetail>.Ok(Detail(template));
    }

    public async Task<PrintTemplateResult<PrintTemplateDetail>> UpdateAsync(
        long id, UpdatePrintTemplateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        PrintTemplate? template = await Find(id, ct);
        if (template is null)
        {
            return PrintTemplateResult<PrintTemplateDetail>.Fail(PrintTemplateOutcome.NotFound);
        }

        // Checked before anything else is touched: a stale save must leave the
        // row exactly as the other editor left it.
        if (request.TemplateVersion != template.TemplateVersion)
        {
            return PrintTemplateResult<PrintTemplateDetail>.Fail(PrintTemplateOutcome.Stale);
        }

        string name = request.TemplateName.Trim();
        if (await NameTakenAsync(template.DocumentTypeCode, name, id, ct))
        {
            return PrintTemplateResult<PrintTemplateDetail>.Fail(PrintTemplateOutcome.NameTaken);
        }

        PrintSettings settings = request.Settings;
        PrintContent content = request.Content;

        PrintTemplateResult<PrintTemplateDetail>? refusal = Prepare(settings, content);
        if (refusal is not null)
        {
            return refusal;
        }

        template.TemplateName = name;
        template.Settings = settings;
        template.Content = content;
        template.TemplateVersion++;

        await _db.SaveChangesAsync(ct);
        return PrintTemplateResult<PrintTemplateDetail>.Ok(Detail(template));
    }

    /// <summary>
    /// Makes one template the default for its document type.
    ///
    /// <b>One transaction, and the database is what enforces it.</b> Clearing
    /// the old default takes a row lock, so a second request doing the same
    /// thing waits rather than interleaving; and the filtered unique index
    /// refuses a second default outright if anything ever slipped past. An
    /// application-level lock would only hold within one process.
    /// </summary>
    public async Task<PrintTemplateOutcome> SetDefaultAsync(long id, CancellationToken ct)
    {
        PrintTemplate? template = await Find(id, ct);
        if (template is null)
        {
            return PrintTemplateOutcome.NotFound;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.PrintTemplates
            .Where(t => t.DocumentTypeCode == template.DocumentTypeCode
                && t.IsDefault
                && t.PrintTemplateId != id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsDefault, false), ct);

        await _db.PrintTemplates
            .Where(t => t.PrintTemplateId == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsDefault, true), ct);

        await transaction.CommitAsync(ct);

        // ExecuteUpdate writes straight to the database and the change tracker
        // never hears about it, so every PrintTemplate this request has already
        // loaded still reports the old flag — including the template that just
        // stopped being the default. A later read in the same request returns
        // the tracked instance, not the row, and the caller sees two defaults.
        // Reloading only the target is not enough; the stale one is the other.
        _db.ChangeTracker.Clear();
        return PrintTemplateOutcome.Ok;
    }

    /// <summary>
    /// Soft delete. The row stays: documents printed against it keep pointing at
    /// it, and resolution falls through to the branch default anyway.
    /// </summary>
    public async Task<PrintTemplateOutcome> DeleteAsync(long id, CancellationToken ct)
    {
        PrintTemplate? template = await Find(id, ct);
        if (template is null)
        {
            return PrintTemplateOutcome.NotFound;
        }

        // Deleting the default, or the last one, would leave a document type
        // with nothing to print.
        int remaining = await _db.PrintTemplates
            .CountAsync(t => t.DocumentTypeCode == template.DocumentTypeCode && t.IsActive, ct);

        if (template.IsDefault || remaining <= 1)
        {
            return PrintTemplateOutcome.LastTemplate;
        }

        template.IsActive = false;
        template.TemplateVersion++;

        await _db.SaveChangesAsync(ct);
        return PrintTemplateOutcome.Ok;
    }

    /// <summary>
    /// Puts the platform's current layout back, keeping the template's name.
    ///
    /// Settings are left alone: a branch that chose A5 chose it deliberately,
    /// and "reset the layout" is not "reset the paper".
    /// </summary>
    public async Task<PrintTemplateResult<PrintTemplateDetail>> ResetAsync(long id, CancellationToken ct)
    {
        PrintTemplate? template = await Find(id, ct);
        if (template is null)
        {
            return PrintTemplateResult<PrintTemplateDetail>.Fail(PrintTemplateOutcome.NotFound);
        }

        DocumentTypeProfile? profile = DocumentTypeCatalog.Find(template.DocumentTypeCode);
        if (profile is null)
        {
            return PrintTemplateResult<PrintTemplateDetail>.Fail(PrintTemplateOutcome.InvalidDocumentType);
        }

        template.Content = DefaultLayoutGenerator.Build(profile);
        template.SeedVersion = DefaultLayoutGenerator.SeedVersion;
        template.TemplateVersion++;

        await _db.SaveChangesAsync(ct);
        return PrintTemplateResult<PrintTemplateDetail>.Ok(Detail(template));
    }

    public async Task<PrintTemplateResult<PrintPreviewResponse>> PreviewAsync(
        long id, PreviewPrintTemplateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SampleDocId is not null)
        {
            // Master cannot read sal, pur or acc. The document's own service
            // renders it, through the same renderer and the same template.
            return PrintTemplateResult<PrintPreviewResponse>.Fail(PrintTemplateOutcome.PreviewNotAvailableHere);
        }

        PrintTemplate? template = await Find(id, ct);
        if (template is null)
        {
            return PrintTemplateResult<PrintPreviewResponse>.Fail(PrintTemplateOutcome.NotFound);
        }

        PrintRenderResult rendered = _renderer.Render(new PrintRenderRequest
        {
            Settings = template.Settings,
            Content = template.Content,
            DocumentTypeCode = template.DocumentTypeCode,
            Payload = SamplePayload.For(template.DocumentTypeCode),
        });

        return PrintTemplateResult<PrintPreviewResponse>.Ok(new PrintPreviewResponse
        {
            Html = rendered.Html,
            PageCount = rendered.PageCount,
            UnknownTags = rendered.UnknownTags,
        });
    }

    /// <summary>
    /// Which layout a document actually prints with: the template it names, then
    /// the branch's default for its type, then the platform's generated layout.
    ///
    /// <b>Every step of that chain has to work, because the first two can
    /// vanish.</b> A template can be soft-deleted after a document named it, and
    /// a branch seeded before a document type was added to the catalogue has no
    /// default for it. Falling through to the generated layout is what keeps a
    /// document printable in both cases rather than erroring on a page somebody
    /// is trying to hand to a customer.
    /// </summary>
    public async Task<PrintTemplateResolution> ResolveAsync(
        string documentTypeCode, long? explicitId, CancellationToken ct)
    {
        string code = documentTypeCode.Trim().ToUpperInvariant();

        if (explicitId is long id)
        {
            PrintTemplate? named = await _db.PrintTemplates
                .FirstOrDefaultAsync(t => t.PrintTemplateId == id && t.DocumentTypeCode == code && t.IsActive, ct);

            if (named is not null)
            {
                return Resolution(named, PrintTemplateSource.Explicit);
            }
        }

        PrintTemplate? fallback = await _db.PrintTemplates
            .FirstOrDefaultAsync(t => t.DocumentTypeCode == code && t.IsDefault && t.IsActive, ct);

        if (fallback is not null)
        {
            return Resolution(fallback, PrintTemplateSource.BranchDefault);
        }

        DocumentTypeProfile? profile = DocumentTypeCatalog.Find(code);

        return new PrintTemplateResolution
        {
            Source = PrintTemplateSource.PlatformSeed,
            DocumentTypeCode = code,
            Settings = DefaultLayoutGenerator.BuildSettings(),
            Content = profile is null ? new PrintContent() : DefaultLayoutGenerator.Build(profile),
        };
    }

    private static PrintTemplateResolution Resolution(PrintTemplate template, PrintTemplateSource source) => new()
    {
        Source = source,
        PrintTemplateId = template.PrintTemplateId,
        DocumentTypeCode = template.DocumentTypeCode.Trim(),
        Settings = template.Settings,
        Content = template.Content,
    };

    /// <summary>Normalises, validates and sanitises in that order. Null means it may be saved.</summary>
    private static PrintTemplateResult<PrintTemplateDetail>? Prepare(PrintSettings settings, PrintContent content)
    {
        PrintSettingsValidator.Normalize(settings);

        IReadOnlyList<GeometryFailure> geometry = PrintSettingsValidator.Validate(settings);
        if (geometry.Count > 0)
        {
            return PrintTemplateResult<PrintTemplateDetail>.Fail(
                PrintTemplateOutcome.InvalidGeometry,
                [.. geometry.Select(f => $"{f.Field}: {f.Message}")]);
        }

        IReadOnlyList<SegmentViolation> violations = new SegmentSanitizer().SanitizeContent(content);
        if (violations.Count > 0)
        {
            return PrintTemplateResult<PrintTemplateDetail>.Fail(
                PrintTemplateOutcome.InvalidSegmentHtml,
                [.. violations.Select(v => v.ToString())]);
        }

        return null;
    }

    private Task<PrintTemplate?> Find(long id, CancellationToken ct) =>
        _db.PrintTemplates.FirstOrDefaultAsync(t => t.PrintTemplateId == id && t.IsActive, ct);

    private Task<bool> NameTakenAsync(string documentTypeCode, string name, long? exceptId, CancellationToken ct) =>
        _db.PrintTemplates.AnyAsync(
            t => t.DocumentTypeCode == documentTypeCode
                && t.IsActive
                && t.TemplateName == name
                && (exceptId == null || t.PrintTemplateId != exceptId),
            ct);

    private static PrintTemplateDetail Detail(PrintTemplate template) => new()
    {
        PrintTemplateId = template.PrintTemplateId,
        DocumentTypeCode = template.DocumentTypeCode.Trim(),
        TemplateName = template.TemplateName,
        IsDefault = template.IsDefault,
        IsActive = template.IsActive,
        Settings = template.Settings,
        Content = template.Content,
        TemplateVersion = template.TemplateVersion,
        SeedVersion = template.SeedVersion,
        CanResetToLatest = template.SeedVersion > 0 && template.SeedVersion < DefaultLayoutGenerator.SeedVersion,
        UnknownTags = MergeTags.Unknown(template.Content, template.DocumentTypeCode.Trim()),
        UpdatedAt = template.ModifiedAt ?? template.CreatedAt,
    };
}

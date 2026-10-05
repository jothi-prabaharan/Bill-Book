using Microsoft.EntityFrameworkCore;
using Printing.Entity.Enums;
using Printing.Entity.Models;
using Printing.Entity.TableEntities;
using Printing.Repository.SeedData;

namespace Printing.Repository.Repositories;

public enum LabelOutcome
{
    Ok,
    NotFound,

    /// <summary>More than <see cref="LabelRendererRepository.MaxLabels"/> labels in one request.</summary>
    TooMany,
}

public sealed record LabelResult<T>(LabelOutcome Outcome, T? Value)
{
    public static LabelResult<T> Ok(T value) => new(LabelOutcome.Ok, value);

    public static LabelResult<T> Refused(LabelOutcome outcome) => new(outcome, default);
}

/// <summary>
/// Label sizes and label printing for the caller's branch (TK-159). The query
/// filter keeps every read to the branch; the labels' content comes from the
/// request, never from another service's tables.
/// </summary>
public sealed class LabelRepository
{
    private readonly PrintingDbContext _db;

    public LabelRepository(PrintingDbContext db) => _db = db;

    public async Task<IReadOnlyList<LabelTemplateView>> ListAsync(CancellationToken ct) =>
        await _db.LabelTemplates.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Kind).ThenByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .Select(t => new LabelTemplateView
            {
                LabelTemplateId = t.LabelTemplateId,
                Kind = t.Kind.ToString(),
                Name = t.Name,
                WidthMm = t.WidthMm,
                HeightMm = t.HeightMm,
                GapMm = t.GapMm,
                Columns = t.Columns,
                ColumnGapMm = t.ColumnGapMm,
                IsDefault = t.IsDefault,
            })
            .ToListAsync(ct);

    /// <summary>Changes a label's size to the branch's own roll. The kind stays.</summary>
    public async Task<LabelOutcome> SaveAsync(long labelTemplateId, SaveLabelTemplateRequest request, CancellationToken ct)
    {
        LabelTemplate? template = await _db.LabelTemplates
            .FirstOrDefaultAsync(t => t.LabelTemplateId == labelTemplateId && t.IsActive, ct);
        if (template is null)
        {
            return LabelOutcome.NotFound;
        }

        template.Name = request.Name.Trim();
        template.WidthMm = request.WidthMm;
        template.HeightMm = request.HeightMm;
        template.GapMm = request.GapMm;
        template.Columns = request.Columns;
        template.ColumnGapMm = request.ColumnGapMm;
        await _db.SaveChangesAsync(ct);
        return LabelOutcome.Ok;
    }

    /// <summary>The labels as a page at the chosen size, or the branch's default for the kind.</summary>
    public async Task<LabelResult<PrintLabelsResponse>> RenderAsync(PrintLabelsRequest request, CancellationToken ct)
    {
        int count = LabelRendererRepository.CountOf(request.Labels);
        if (count > LabelRendererRepository.MaxLabels)
        {
            return LabelResult<PrintLabelsResponse>.Refused(LabelOutcome.TooMany);
        }

        LabelTemplate? template = request.LabelTemplateId is long id
            ? await _db.LabelTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.LabelTemplateId == id && t.IsActive, ct)
            : await _db.LabelTemplates.AsNoTracking()
                .Where(t => t.Kind == request.Kind && t.IsActive)
                .OrderByDescending(t => t.IsDefault)
                .FirstOrDefaultAsync(ct);

        if (request.LabelTemplateId is not null && template is null)
        {
            return LabelResult<PrintLabelsResponse>.Refused(LabelOutcome.NotFound);
        }

        // A chosen size prints the chosen kind's content: the size belongs to
        // the template, what is on it to the request.
        LabelSize size = template is null
            ? LabelTemplateSeed.SizeFor(request.Kind)
            : new LabelSize(template.WidthMm, template.HeightMm, template.GapMm, template.Columns, template.ColumnGapMm);

        return LabelResult<PrintLabelsResponse>.Ok(new PrintLabelsResponse
        {
            Html = LabelRendererRepository.Render(request.Kind, size, request.Labels, request.Sheet),
            Count = count,
        });
    }

    /// <summary>The default sizes a branch is missing. Idempotent; never overwrites a size the branch set.</summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        List<LabelKind> present = await _db.LabelTemplates
            .IgnoreQueryFilters()
            .Where(t => t.OrgId == orgId && t.IsDefault && t.IsActive)
            .Select(t => t.Kind)
            .ToListAsync(ct);

        IReadOnlyList<LabelTemplate> missing = LabelTemplateSeed.Build(orgId, present);
        if (missing.Count == 0)
        {
            return 0;
        }

        _db.LabelTemplates.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }
}

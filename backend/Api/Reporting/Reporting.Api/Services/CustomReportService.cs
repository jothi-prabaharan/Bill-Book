using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Entity.TableEntities;
using Reporting.Repository;
using Shared.Kernel.Interfaces;

namespace Reporting.Api.Services;

/// <summary>
/// Custom reports (TK-113): a dataset plus a saved layout, private to its
/// owner or shared with the branch.
///
/// <b>The dataset's permission is the report's.</b> Every read goes through
/// <see cref="ReportCatalogService.Source"/> with the caller's permissions, so
/// a shared purchase-lines report is absent for someone without
/// <c>purchase.view</c> — in the catalog, on open and on run — exactly as the
/// dataset is.
/// </summary>
public sealed class CustomReportService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ReportingDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ReportCatalogService _catalog;

    public CustomReportService(ReportingDbContext db, ICurrentUser user, ReportCatalogService catalog)
    {
        _db = db;
        _user = user;
        _catalog = catalog;
    }

    /// <summary>The custom reports this caller may open, as catalog entries under <see cref="ReportModule.Custom"/>.</summary>
    public async Task<List<ReportCatalogItemView>> CatalogAsync(IReadOnlySet<string> permissions, CancellationToken ct)
    {
        List<CustomReport> reports = await Visible().OrderBy(r => r.Title).ToListAsync(ct);

        return
        [
            .. reports
                .Where(r => Dataset(r.DatasetKey, permissions) is not null)
                .Select((r, i) => new ReportCatalogItemView
                {
                    ReportKey = r.DatasetKey,
                    Title = r.Title,
                    Module = ReportModule.Custom,
                    Description = r.Description,
                    SortOrder = 10_000 + i,
                    CustomReportId = r.CustomReportId,
                }),
        ];
    }

    /// <summary>
    /// One custom report, with any column its dataset no longer has taken out
    /// of the layout and named in <see cref="CustomReportModel.DroppedColumns"/>.
    /// Null when it does not exist, is someone else's private report, or reads
    /// a dataset the caller may not see.
    /// </summary>
    public async Task<CustomReportModel?> GetAsync(long id, bool mayShare, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        CustomReport? report = await Visible().FirstOrDefaultAsync(r => r.CustomReportId == id, ct);

        if (report is null || Dataset(report.DatasetKey, permissions) is not IReportSource source)
        {
            return null;
        }

        IReadOnlyDictionary<string, string> labels = await PrepareAsync(source, ct);
        ReportQueryRequest layout = JsonSerializer.Deserialize<ReportQueryRequest>(report.DefinitionJson, Json) ?? new();
        List<string> dropped = Prune(layout, KnownKeys(source));

        return new CustomReportModel
        {
            CustomReportId = report.CustomReportId,
            DatasetKey = report.DatasetKey,
            Title = report.Title,
            Description = report.Description,
            IsShared = report.OwnerUserId is null,
            MayEdit = MayEdit(report, mayShare),
            Layout = layout,
            DroppedColumns = [.. dropped.Select(key => labels.TryGetValue(key, out string? label) ? label : key)],
        };
    }

    public async Task<CustomReportModel?> CreateAsync(
        CustomReportModel model, bool mayShare, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        if (Dataset(model.DatasetKey, permissions) is not IReportSource source)
        {
            return null;
        }

        await CheckAsync(model, source, mayShare, ct);

        CustomReport report = new()
        {
            DatasetKey = source.ReportKey,
            Title = model.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            OwnerUserId = model.IsShared ? null : _user.UserId,
            DefinitionJson = JsonSerializer.Serialize(Shareable(model.Layout), Json),
        };

        _db.CustomReports.Add(report);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(report.CustomReportId, mayShare, permissions, ct);
    }

    /// <summary>
    /// Changes the title, description, sharing or layout. The dataset is fixed:
    /// a report that reads another dataset is another report.
    /// </summary>
    public async Task<CustomReportModel?> UpdateAsync(
        long id, CustomReportModel model, bool mayShare, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        CustomReport? report = await Visible().FirstOrDefaultAsync(r => r.CustomReportId == id, ct);

        if (report is null || !MayEdit(report, mayShare) || Dataset(report.DatasetKey, permissions) is not IReportSource source)
        {
            return null;
        }

        await CheckAsync(model, source, mayShare, ct);

        report.Title = model.Title.Trim();
        report.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        report.OwnerUserId = model.IsShared ? null : report.OwnerUserId ?? _user.UserId;
        report.DefinitionJson = JsonSerializer.Serialize(Shareable(model.Layout), Json);

        await _db.SaveChangesAsync(ct);

        return await GetAsync(report.CustomReportId, mayShare, permissions, ct);
    }

    /// <summary>Deactivates it. Its layout is kept, as a deleted saved report is still an audit record.</summary>
    public async Task<bool> DeleteAsync(long id, bool mayShare, CancellationToken ct)
    {
        CustomReport? report = await Visible().FirstOrDefaultAsync(r => r.CustomReportId == id, ct);

        if (report is null || !MayEdit(report, mayShare))
        {
            return false;
        }

        report.IsActive = false;
        await _db.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>
    /// Removes from <paramref name="layout"/> every column, filter, sort, group
    /// and pivot entry naming a key not in <paramref name="known"/>, and returns
    /// those keys. Pure: the rule a deactivated field's column goes by.
    /// </summary>
    public static List<string> Prune(ReportQueryRequest layout, IReadOnlySet<string> known)
    {
        SortedSet<string> dropped = new(StringComparer.Ordinal);

        bool Keep(string key)
        {
            if (known.Contains(key))
            {
                return true;
            }

            dropped.Add(key);
            return false;
        }

        layout.Columns = [.. layout.Columns.Where(Keep)];
        layout.Filters = [.. layout.Filters.Where(f => Keep(f.Column))];
        layout.Sorts = [.. layout.Sorts.Where(s => Keep(s.Column))];
        layout.GroupBy = [.. layout.GroupBy.Where(Keep)];

        if (layout.Pivot is ReportPivotModel pivot)
        {
            pivot.Rows = [.. pivot.Rows.Where(Keep)];
            pivot.Columns = [.. pivot.Columns.Where(Keep)];
            pivot.Values = [.. pivot.Values.Where(v => Keep(v.Column))];

            if (pivot.Rows.Count == 0 || pivot.Values.Count == 0)
            {
                layout.Pivot = null;
            }
        }

        return [.. dropped];
    }

    private IQueryable<CustomReport> Visible()
    {
        Guid? userId = _user.UserId;

        return _db.CustomReports.Where(r => r.IsActive && (r.OwnerUserId == null || r.OwnerUserId == userId));
    }

    private IReportSource? Dataset(string datasetKey, IReadOnlySet<string> permissions) =>
        _catalog.Source(datasetKey, permissions) is { IsDataset: true } source ? source : null;

    private bool MayEdit(CustomReport report, bool mayShare) =>
        report.OwnerUserId is null ? mayShare : report.OwnerUserId == _user.UserId;

    private static async Task<IReadOnlyDictionary<string, string>> PrepareAsync(IReportSource source, CancellationToken ct)
    {
        if (source is not IDatasetSource dataset)
        {
            return new Dictionary<string, string>();
        }

        await dataset.PrepareColumnsAsync(ct);
        return dataset.CustomLabels;
    }

    private static HashSet<string> KnownKeys(IReportSource source) =>
        [.. source.Columns.Select(c => c.Key)];

    /// <summary>
    /// A new layout may only name columns the dataset has now. Saving one that
    /// names a missing column is refused rather than pruned, because the person
    /// saving is looking at it and can fix it.
    /// </summary>
    private static async Task CheckAsync(CustomReportModel model, IReportSource source, bool mayShare, CancellationToken ct)
    {
        if (model.IsShared && !mayShare)
        {
            throw new ReportQueryException("Sharing a custom report with the whole branch needs the reports.edit permission.");
        }

        await PrepareAsync(source, ct);

        ReportQueryRequest copy = JsonSerializer.Deserialize<ReportQueryRequest>(JsonSerializer.Serialize(model.Layout, Json), Json) ?? new();
        List<string> unknown = Prune(copy, KnownKeys(source));

        if (unknown.Count > 0)
        {
            throw new ReportQueryException($"{source.Title} has no column {string.Join(", ", unknown)}.");
        }
    }

    /// <summary>The page number is not saved, as with a saved view.</summary>
    private static ReportQueryRequest Shareable(ReportQueryRequest layout)
    {
        layout.Page = new ReportPageModel
        {
            Number = 1,
            Size = layout.Page.Size,
            IncludeCount = true,
        };

        return layout;
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Entity.TableEntities;
using Reporting.Repository;
using Shared.Kernel.Interfaces;

namespace Reporting.Api.Services;

/// <summary>
/// Saved layouts — which columns, filters, sorting, grouping and freezing a
/// person or a branch wants a report opened with.
///
/// Reports here carry eight to forty columns, so without this every user re-picks
/// them on every visit.
/// </summary>
public sealed class SavedViewService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ReportingDbContext _db;
    private readonly ICurrentUser _user;

    public SavedViewService(ReportingDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    /// <summary>
    /// This user's views for a report, plus the branch-wide ones.
    ///
    /// Another person's private view is never returned — it is theirs, and a list
    /// of everyone's working layouts is noise at best.
    /// </summary>
    public async Task<List<SavedViewModel>> ListAsync(string reportKey, CancellationToken ct)
    {
        Guid? userId = _user.UserId;

        List<ReportView> views = await Views(reportKey)
            .Where(v => v.OwnerUserId == null || v.OwnerUserId == userId)
            .OrderBy(v => v.OwnerUserId == null)
            .ThenBy(v => v.ViewName)
            .ToListAsync(ct);

        return [.. views.Select(Model)];
    }

    /// <summary>
    /// Saves a new layout.
    ///
    /// <b>The owner comes from the token, never from the caller.</b> Accepting one
    /// would let anybody write a view into somebody else's list, or publish a
    /// branch-wide one by sending a null.
    /// </summary>
    public async Task<SavedViewModel?> CreateAsync(
        string reportKey, SavedViewModel model, bool mayShare, CancellationToken ct)
    {
        long? reportId = await ReportIdAsync(reportKey, ct);

        if (reportId is not long id)
        {
            return null;
        }

        if (model.IsShared && !mayShare)
        {
            throw new ReportQueryException(
                "Publishing a view to the whole branch needs the reports.edit permission.");
        }

        ReportView view = new()
        {
            ReportId = id,
            ViewName = model.ViewName.Trim(),
            OwnerUserId = model.IsShared ? null : _user.UserId,
            IsDefault = model.IsDefault,
            LayoutJson = JsonSerializer.Serialize(Shareable(model.Layout), Json),
        };

        if (view.IsDefault)
        {
            await ClearDefaultAsync(id, view.OwnerUserId, ct);
        }

        _db.ReportViews.Add(view);
        await _db.SaveChangesAsync(ct);

        return Model(view);
    }

    public async Task<SavedViewModel?> UpdateAsync(
        string reportKey, long viewId, SavedViewModel model, bool mayShare, CancellationToken ct)
    {
        ReportView? view = await Views(reportKey).FirstOrDefaultAsync(v => v.ReportViewId == viewId, ct);

        if (view is null || !MayEdit(view, mayShare))
        {
            return null;
        }

        view.ViewName = model.ViewName.Trim();
        view.LayoutJson = JsonSerializer.Serialize(Shareable(model.Layout), Json);

        if (model.IsDefault && !view.IsDefault)
        {
            await ClearDefaultAsync(view.ReportId, view.OwnerUserId, ct);
        }

        view.IsDefault = model.IsDefault;

        await _db.SaveChangesAsync(ct);

        return Model(view);
    }

    public async Task<bool> DeleteAsync(
        string reportKey, long viewId, bool mayShare, CancellationToken ct)
    {
        ReportView? view = await Views(reportKey).FirstOrDefaultAsync(v => v.ReportViewId == viewId, ct);

        if (view is null || !MayEdit(view, mayShare))
        {
            return false;
        }

        _db.ReportViews.Remove(view);
        await _db.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>
    /// A branch-wide view may only be changed by somebody who could have published
    /// it; a private one only by its owner. The query filter has already limited
    /// this to the branch, so what is left is whose view it is.
    /// </summary>
    private bool MayEdit(ReportView view, bool mayShare) =>
        view.OwnerUserId is null ? mayShare : view.OwnerUserId == _user.UserId;

    /// <summary>
    /// Clears the previous default before setting a new one.
    ///
    /// The database also has a filtered unique index on this, which is the part
    /// that actually holds: two requests setting a default at once both pass this
    /// check and only one passes the index.
    /// </summary>
    private async Task ClearDefaultAsync(long reportId, Guid? ownerId, CancellationToken ct) =>
        await _db.ReportViews
            .Where(v => v.ReportId == reportId && v.OwnerUserId == ownerId && v.IsDefault)
            .ExecuteUpdateAsync(v => v.SetProperty(x => x.IsDefault, false), ct);

    private IQueryable<ReportView> Views(string reportKey) =>
        _db.ReportViews.Where(v =>
            _db.Reports.Any(r => r.ReportId == v.ReportId && r.ReportKey == reportKey));

    private async Task<long?> ReportIdAsync(string reportKey, CancellationToken ct) =>
        await _db.Reports
            .Where(r => r.ReportKey == reportKey)
            .Select(r => (long?)r.ReportId)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// The page number is not saved. A view is "how I like this report laid out",
    /// not "page nine of it" — restoring somebody onto page nine of a result that
    /// has since changed shows them rows they were not looking for, or nothing.
    /// </summary>
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

    private static SavedViewModel Model(ReportView view) => new()
    {
        ReportViewId = view.ReportViewId,
        ViewName = view.ViewName,
        IsShared = view.OwnerUserId is null,
        IsDefault = view.IsDefault,
        Layout = JsonSerializer.Deserialize<ReportQueryRequest>(view.LayoutJson, Json) ?? new(),
    };
}

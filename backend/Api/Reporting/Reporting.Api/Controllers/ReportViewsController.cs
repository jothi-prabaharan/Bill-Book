using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Services;
using Reporting.Entity.Models;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Saved layouts for a report.
///
/// Separate from <see cref="ReportsController"/> because these are writes and
/// those are reads — and because the two have different authorities: running a
/// report needs <c>reports.view</c>, publishing a layout to the whole branch needs
/// <c>reports.edit</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("reports")]
[Route("api/reports/{reportKey}/views")]
public sealed class ReportViewsController : ControllerBase
{
    private readonly SavedViewService _views;

    public ReportViewsController(SavedViewService views) => _views = views;

    /// <summary>This user's views, and the branch's. Never anybody else's private ones.</summary>
    [HttpGet]
    public async Task<IActionResult> List(string reportKey, CancellationToken ct) =>
        Ok(await _views.ListAsync(reportKey, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        string reportKey, [FromBody] SavedViewModel model, CancellationToken ct)
    {
        try
        {
            SavedViewModel? saved = await _views.CreateAsync(reportKey, model, MayShare(), ct);

            return saved is null ? NotFound() : Ok(saved);
        }
        catch (ReportQueryException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{viewId:long}")]
    public async Task<IActionResult> Update(
        string reportKey, long viewId, [FromBody] SavedViewModel model, CancellationToken ct)
    {
        SavedViewModel? saved = await _views.UpdateAsync(reportKey, viewId, model, MayShare(), ct);

        // NotFound covers "no such view", "another branch's" and "not yours to
        // edit" alike. Distinguishing them would tell a caller what exists.
        return saved is null ? NotFound() : Ok(saved);
    }

    [HttpDelete("{viewId:long}")]
    public async Task<IActionResult> Delete(string reportKey, long viewId, CancellationToken ct) =>
        await _views.DeleteAsync(reportKey, viewId, MayShare(), ct) ? NoContent() : NotFound();

    /// <summary>
    /// Whether this caller may publish a layout to the whole branch.
    ///
    /// Read from the token's claims rather than inferred from the route's own
    /// permission: every route here already required <c>reports.view</c>, and a
    /// branch-wide view is a different authority from a personal one.
    /// </summary>
    private bool MayShare() =>
        User.FindAll("permission").Any(claim => claim.Value == "reports.edit");
}

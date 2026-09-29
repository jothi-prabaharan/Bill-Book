using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Services;
using Reporting.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Custom reports built from a dataset (TK-113). They run through the
/// ordinary <c>api/reports/{datasetKey}/query</c> route with the saved layout;
/// these routes only keep the layouts.
///
/// Every route is <c>reports.view</c>, the permission a saved view needs too:
/// saving a private report is a reader's convenience, not an edit to the
/// books. Sharing one with the branch is the <c>reports.edit</c> claim,
/// checked on the token as <see cref="ReportViewsController"/> does.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("reports")]
[Route("api/reports/custom")]
[RequireApp(App.RetailErp)]
public sealed class CustomReportsController : ControllerBase
{
    private readonly CustomReportService _reports;

    public CustomReportsController(CustomReportService reports) => _reports = reports;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _reports.CatalogAsync(Permissions(), ct));

    /// <summary>
    /// One report and its layout. NotFound covers "no such report", "someone
    /// else's private one" and "a dataset you may not see" alike.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _reports.GetAsync(id, MayShare(), Permissions(), ct) is CustomReportModel report ? Ok(report) : NotFound();

    [HttpPost]
    [PermissionAction("view")]
    public async Task<IActionResult> Create([FromBody] CustomReportModel model, CancellationToken ct)
    {
        try
        {
            CustomReportModel? saved = await _reports.CreateAsync(model, MayShare(), Permissions(), ct);
            return saved is null ? NotFound() : Ok(saved);
        }
        catch (ReportQueryException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:long}")]
    [PermissionAction("view")]
    public async Task<IActionResult> Update(long id, [FromBody] CustomReportModel model, CancellationToken ct)
    {
        try
        {
            CustomReportModel? saved = await _reports.UpdateAsync(id, model, MayShare(), Permissions(), ct);
            return saved is null ? NotFound() : Ok(saved);
        }
        catch (ReportQueryException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    [PermissionAction("view")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        await _reports.DeleteAsync(id, MayShare(), ct) ? NoContent() : NotFound();

    private bool MayShare() =>
        User.FindAll("permission").Any(claim => claim.Value == "reports.edit");

    private IReadOnlySet<string> Permissions() =>
        User.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
}

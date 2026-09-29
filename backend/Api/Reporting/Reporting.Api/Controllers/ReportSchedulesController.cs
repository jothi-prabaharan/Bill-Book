using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Services;
using Reporting.Api.Services.Schedules;
using Reporting.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Report schedules (TK-114). Listing needs <c>reports.view</c>; making,
/// changing or deleting one needs <c>reports.export</c>, because a schedule is
/// an export that repeats. The report's own module permission is checked on
/// save here and again at every run, as the owner's permission then.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("reports")]
[Route("api/reports/schedules")]
[RequireApp(App.RetailErp)]
public sealed class ReportSchedulesController : ControllerBase
{
    private readonly ReportScheduleService _schedules;

    public ReportSchedulesController(ReportScheduleService schedules) => _schedules = schedules;

    /// <summary>The caller's own schedules, for one report or all.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? reportKey, CancellationToken ct) =>
        Ok(await _schedules.ListAsync(reportKey, ct));

    /// <summary>The branch's users a schedule can be sent to.</summary>
    [HttpGet("recipients")]
    public async Task<IActionResult> Recipients(CancellationToken ct) =>
        await _schedules.RecipientsAsync(ct) is { } recipients
            ? Ok(recipients)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The branch's users could not be read just now." });

    [HttpPost]
    [PermissionAction("export")]
    public async Task<IActionResult> Create([FromBody] ReportScheduleModel model, CancellationToken ct)
    {
        try
        {
            ReportScheduleModel? saved = await _schedules.CreateAsync(model, Permissions(), ct);
            return saved is null ? NotFound() : Ok(saved);
        }
        catch (ReportQueryException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:long}")]
    [PermissionAction("export")]
    public async Task<IActionResult> Update(long id, [FromBody] ReportScheduleModel model, CancellationToken ct)
    {
        try
        {
            ReportScheduleModel? saved = await _schedules.UpdateAsync(id, model, Permissions(), ct);
            return saved is null ? NotFound() : Ok(saved);
        }
        catch (ReportQueryException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    [PermissionAction("export")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        await _schedules.DeleteAsync(id, ct) ? NoContent() : NotFound();

    private IReadOnlySet<string> Permissions() =>
        User.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
}

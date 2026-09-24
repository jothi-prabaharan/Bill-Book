using Hrm.Api.Services;
using Hrm.Entity.Enums;
using Hrm.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Hrm.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("employee")]
[RequireApp(App.Hrms | App.Payroll | App.School)]
[Route("api/hrm/lifecycle")]
public sealed class LifecycleController : ControllerBase
{
    private readonly LifecycleService _lifecycle;

    public LifecycleController(LifecycleService lifecycle) => _lifecycle = lifecycle;

    // ---- Templates ----
    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates([FromQuery] ChecklistKind? kind, CancellationToken ct) =>
        Ok(await _lifecycle.ListTemplatesAsync(kind, ct));

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] SaveChecklistTemplateRequest request, CancellationToken ct) =>
        Ok(new { id = await _lifecycle.SaveTemplateAsync(null, request, ct) });

    [HttpPut("templates/{id:long}")]
    public async Task<IActionResult> UpdateTemplate(long id, [FromBody] SaveChecklistTemplateRequest request, CancellationToken ct) =>
        Ok(new { id = await _lifecycle.SaveTemplateAsync(id, request, ct) });

    // ---- Checklists ----
    [HttpGet("checklists/{employeeId:long}")]
    public async Task<IActionResult> GetChecklist(long employeeId, [FromQuery] ChecklistKind kind = ChecklistKind.Onboarding, CancellationToken ct = default)
    {
        var checklist = await _lifecycle.GetEmployeeChecklistAsync(employeeId, kind, ct);
        return checklist is null ? NotFound() : Ok(checklist);
    }

    [HttpPost("checklists")]
    public async Task<IActionResult> CreateChecklist([FromBody] CreateEmployeeChecklistRequest request, CancellationToken ct) =>
        Ok(new { id = await _lifecycle.CreateEmployeeChecklistAsync(request, ct) });

    [HttpPut("checklists/items/{itemId:long}")]
    public async Task<IActionResult> UpdateChecklistItem(long itemId, [FromBody] UpdateChecklistItemRequest request, CancellationToken ct)
    {
        await _lifecycle.UpdateChecklistItemAsync(itemId, request, ct);
        return NoContent();
    }

    // ---- Separation ----
    [HttpGet("separations/{employeeId:long}")]
    public async Task<IActionResult> GetSeparation(long employeeId, CancellationToken ct)
    {
        var separation = await _lifecycle.GetSeparationAsync(employeeId, ct);
        return separation is null ? NotFound() : Ok(separation);
    }

    [HttpPost("separations")]
    public async Task<IActionResult> SubmitSeparation([FromBody] SaveSeparationRequest request, CancellationToken ct) =>
        Ok(new { id = await _lifecycle.SubmitSeparationAsync(request, ct) });

    [HttpPost("separations/{id:long}/approve")]
    public async Task<IActionResult> ApproveSeparation(long id, CancellationToken ct)
    {
        await _lifecycle.ApproveSeparationAsync(id, ct);
        return NoContent();
    }

    [HttpPost("separations/{id:long}/clear")]
    public async Task<IActionResult> ClearSeparation(long id, CancellationToken ct)
    {
        await _lifecycle.ClearSeparationAsync(id, ct);
        return NoContent();
    }

    [HttpPost("separations/{employeeId:long}/settle")]
    public async Task<IActionResult> SettleSeparation(long employeeId, [FromQuery] DateOnly? lastWorkingDate, CancellationToken ct)
    {
        await _lifecycle.SettleSeparationAsync(employeeId, lastWorkingDate, ct);
        return NoContent();
    }
}

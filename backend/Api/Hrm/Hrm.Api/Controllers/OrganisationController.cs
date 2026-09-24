using Hrm.Api.Services;
using Hrm.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Hrm.Api.Controllers;

/// <summary>
/// Organisation setup (H1, TK-48): departments, designations, grades, cost
/// centres and work locations. Part of the employee master, so it serves HRMS,
/// Payroll and School, under the <c>employee</c> module. Reading takes
/// <c>employee.view</c> and saving <c>employee.edit</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("employee")]
[RequireApp(App.Hrms | App.Payroll | App.School)]
[Route("api/hrm/organisation")]
public sealed class OrganisationController : ControllerBase
{
    private readonly OrganisationService _organisation;

    public OrganisationController(OrganisationService organisation) => _organisation = organisation;

    [HttpGet("departments")]
    public async Task<IActionResult> Departments(CancellationToken ct) => Ok(await _organisation.DepartmentsAsync(ct));

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment([FromBody] SaveDepartmentRequest request, CancellationToken ct) =>
        Answer(await _organisation.SaveDepartmentAsync(null, request, ct));

    [HttpPut("departments/{id:long}")]
    public async Task<IActionResult> UpdateDepartment(long id, [FromBody] SaveDepartmentRequest request, CancellationToken ct) =>
        Answer(await _organisation.SaveDepartmentAsync(id, request, ct));

    [HttpGet("designations")]
    public async Task<IActionResult> Designations(CancellationToken ct) => Ok(await _organisation.DesignationsAsync(ct));

    [HttpPost("designations")]
    public async Task<IActionResult> CreateDesignation([FromBody] SaveOrgMasterRequest request, CancellationToken ct) =>
        Answer(await _organisation.SaveDesignationAsync(null, request, ct));

    [HttpPut("designations/{id:long}")]
    public async Task<IActionResult> UpdateDesignation(long id, [FromBody] SaveOrgMasterRequest request, CancellationToken ct) =>
        Answer(await _organisation.SaveDesignationAsync(id, request, ct));

    [HttpGet("grades")]
    public async Task<IActionResult> Grades(CancellationToken ct) => Ok(await _organisation.GradesAsync(ct));

    [HttpPost("grades")]
    public async Task<IActionResult> CreateGrade([FromBody] SaveGradeRequest request, CancellationToken ct) =>
        Answer(await _organisation.SaveGradeAsync(null, request, ct));

    [HttpPut("grades/{id:long}")]
    public async Task<IActionResult> UpdateGrade(long id, [FromBody] SaveGradeRequest request, CancellationToken ct) =>
        Answer(await _organisation.SaveGradeAsync(id, request, ct));

    [HttpGet("cost-centres")]
    public async Task<IActionResult> CostCentres(CancellationToken ct) => Ok(await _organisation.CostCentresAsync(ct));

    [HttpPost("cost-centres")]
    public async Task<IActionResult> CreateCostCentre([FromBody] SaveOrgMasterRequest request, CancellationToken ct) =>
        Answer(await _organisation.SaveCostCentreAsync(null, request, ct));

    [HttpPut("cost-centres/{id:long}")]
    public async Task<IActionResult> UpdateCostCentre(long id, [FromBody] SaveOrgMasterRequest request, CancellationToken ct) =>
        Answer(await _organisation.SaveCostCentreAsync(id, request, ct));

    [HttpGet("work-locations")]
    public async Task<IActionResult> WorkLocations(CancellationToken ct) => Ok(await _organisation.WorkLocationsAsync(ct));

    [HttpPost("work-locations")]
    public async Task<IActionResult> CreateWorkLocation([FromBody] SaveWorkLocationRequest request, CancellationToken ct) =>
        Answer(await _organisation.SaveWorkLocationAsync(null, request, ct));

    [HttpPut("work-locations/{id:long}")]
    public async Task<IActionResult> UpdateWorkLocation(long id, [FromBody] SaveWorkLocationRequest request, CancellationToken ct) =>
        Answer(await _organisation.SaveWorkLocationAsync(id, request, ct));

    // A row that is not in the caller's branch is not found: the query filter
    // and RLS hide it, so "another branch's" and "none" are one answer.
    private IActionResult Answer(OrgMasterResult result) => result.Outcome switch
    {
        OrgMasterOutcome.Ok => Ok(new { id = result.Id }),
        OrgMasterOutcome.NotFound => NotFound(),
        OrgMasterOutcome.DuplicateCode => Conflict(new HrmMessage("Another entry in this branch already uses that code.")),
        _ => UnprocessableEntity(new HrmMessage("Choose a head or parent from this branch, and never a department under itself.")),
    };
}

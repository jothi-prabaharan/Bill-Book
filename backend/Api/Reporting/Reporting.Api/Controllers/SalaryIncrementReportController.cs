using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Salary Increment Report (TK-198): its own endpoint, request and answer,
/// for its own page, rather than <c>api/reports/{reportKey}/query</c>.
///
/// Payroll's report, so the Payroll add-on and <c>payroll.view</c>, the same
/// as the Salary Register, on any branch with that add-on, a trade or none, as
/// Payroll's own controllers are. <b>No <c>[RequireBusinessType]</c> beside the
/// add-on:</b> <c>ScopeGuard</c> admits a controller carrying both when either
/// matches, so one that allows every trade would wave through a branch without
/// Payroll. The rows are the token's branch's through the query filter; a
/// request naming another branch is refused with 403 rather than answered with
/// that branch's rows or with nothing.
/// </summary>
[ApiController]
[Authorize]
[Route("api/reports/salary-increment")]
[RequireAddOn(AddOn.Payroll)]
[RequireModulePermission("payroll")]
public sealed class SalaryIncrementReportController : ControllerBase
{
    private readonly SalaryIncrementReportRepository _report;

    public SalaryIncrementReportController(SalaryIncrementReportRepository report) => _report = report;

    /// <summary>The employees and departments the filters offer.</summary>
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        Ok(await _report.OptionsAsync(ct));

    /// <summary>Runs the report. A read wearing a POST, so it asks for <c>payroll.view</c>.</summary>
    [HttpPost]
    [PermissionAction("view")]
    public async Task<IActionResult> Run([FromBody] SalaryIncrementReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        if (SalaryIncrementReportRepository.RangeRefusal(request) is string refusal)
        {
            ModelState.AddModelError(nameof(SalaryIncrementReportRequest.ToDate), refusal);
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }
}

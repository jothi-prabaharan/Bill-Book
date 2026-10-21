using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Budget vs Actual by Project (TK-207): its own endpoint, request and answer,
/// for its own page at <c>/reports/budget-vs-actual-by-project</c>, and the
/// projects it can be narrowed to. The budgets it offers are Budget vs
/// Actual's (<c>api/statements/budget-vs-actual/budgets</c>), the ones kept by
/// project. The gateway routes both exact paths to Reporting.
///
/// Guarded as Budget vs Actual is: the Budget add-on alone (with the
/// business-type guard beside it, either would admit), the reports module and
/// <c>accounting.view</c>. A request naming another branch is refused with 403,
/// a budget that is not the branch's is 404, and a budget not kept by project,
/// a date outside its year or a project not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/budget-vs-actual-by-project")]
[RequireAddOn(AddOn.Budget)]
[RequireModulePermission("reports")]
public sealed class BudgetVsActualByProjectReportController : ControllerBase
{
    private readonly BudgetVsActualByProjectReportRepository _report;
    private readonly BudgetVsActualReportRepository _budgets;

    public BudgetVsActualByProjectReportController(
        BudgetVsActualByProjectReportRepository report, BudgetVsActualReportRepository budgets)
    {
        _report = report;
        _budgets = budgets;
    }

    /// <summary>Each project's budget against its postings for the months the as-at date and period cover.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] BudgetVsActualByProjectReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        BudgetVsActualBudgetDto? budget = await _budgets.FindBudgetAsync(request.BudgetId!.Value, ct);
        if (budget is null)
        {
            return NotFound();
        }

        if (BudgetVsActualByProjectReportRepository.BudgetRefusal(budget) is string budgetRefusal)
        {
            ModelState.AddModelError(nameof(BudgetVsActualByProjectReportRequest.BudgetId), budgetRefusal);
        }

        if (BudgetVsActualReportRepository.DateRefusal(budget, request.AsOfDate!.Value) is string dateRefusal)
        {
            ModelState.AddModelError(nameof(BudgetVsActualByProjectReportRequest.AsOfDate), dateRefusal);
        }

        if (request.ProjectId is long projectId && await _report.FindProjectAsync(projectId, ct) is null)
        {
            ModelState.AddModelError(nameof(BudgetVsActualByProjectReportRequest.ProjectId), "That project is not on this branch.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(budget, request, ct));
    }

    /// <summary>The branch's projects, for the page's project filter.</summary>
    [HttpGet("projects")]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Projects(CancellationToken ct) => Ok(await _report.ProjectsAsync(ct));
}

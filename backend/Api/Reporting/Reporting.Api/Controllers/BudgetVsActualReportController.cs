using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Budget vs Actual (TK-204): its own endpoint, request and answer, for its own
/// page at <c>/reports/budget-vs-actual</c>, and the budgets it can be run
/// against. The gateway routes both exact paths to Reporting.
///
/// Guarded by the Budget add-on, as the catalog's budget reports are, with the
/// reports module and <c>accounting.view</c>. <b>No <c>[RequireBusinessType]</c>
/// beside the add-on</b>: the two together admit a branch that has either, which
/// would let a branch without budgets in. The rows are the token's branch's
/// through the query filter: a request naming another branch is refused with
/// 403, and a budget that is not the branch's is 404.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/budget-vs-actual")]
[RequireAddOn(AddOn.Budget)]
[RequireModulePermission("reports")]
public sealed class BudgetVsActualReportController : ControllerBase
{
    private readonly BudgetVsActualReportRepository _report;

    public BudgetVsActualReportController(BudgetVsActualReportRepository report) => _report = report;

    /// <summary>The budget against the books for the months the as-at date and period cover.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] BudgetVsActualReportRequest request, CancellationToken ct)
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

        BudgetVsActualBudgetDto? budget = await _report.FindBudgetAsync(request.BudgetId!.Value, ct);
        if (budget is null)
        {
            return NotFound();
        }

        if (BudgetVsActualReportRepository.DateRefusal(budget, request.AsOfDate!.Value) is string refusal)
        {
            ModelState.AddModelError(nameof(BudgetVsActualReportRequest.AsOfDate), refusal);
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(budget, request, ct));
    }

    /// <summary>The branch's budgets, for the page's budget filter.</summary>
    [HttpGet("budgets")]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Budgets(CancellationToken ct) => Ok(await _report.BudgetsAsync(ct));
}

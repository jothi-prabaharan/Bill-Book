using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Budget Monthly Trend (TK-214): its own endpoint, request and answer, for its
/// own page at <c>/reports/budget-monthly-trend</c>. The budgets it offers are
/// Budget vs Actual's (<c>api/statements/budget-vs-actual/budgets</c>), so it
/// has no lookup of its own. The gateway routes the exact path to Reporting.
///
/// Guarded as Budget vs Actual is: the Budget add-on alone (with the
/// business-type guard beside it, either would admit), the reports module and
/// <c>accounting.view</c>. A request naming another branch is refused with 403,
/// a budget that is not the branch's is 404, and a date outside the budget's
/// year is a field error on <c>AsOfDate</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/budget-monthly-trend")]
[RequireAddOn(AddOn.Budget)]
[RequireModulePermission("reports")]
public sealed class BudgetMonthlyTrendReportController : ControllerBase
{
    private readonly BudgetMonthlyTrendReportRepository _report;
    private readonly BudgetVsActualReportRepository _budgets;

    public BudgetMonthlyTrendReportController(
        BudgetMonthlyTrendReportRepository report, BudgetVsActualReportRepository budgets)
    {
        _report = report;
        _budgets = budgets;
    }

    /// <summary>The budget against the books for each month of its year, with the year to date.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] BudgetMonthlyTrendReportRequest request, CancellationToken ct)
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

        if (BudgetVsActualReportRepository.DateRefusal(budget, request.AsOfDate!.Value) is string refusal)
        {
            ModelState.AddModelError(nameof(BudgetMonthlyTrendReportRequest.AsOfDate), refusal);
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(budget, request, ct));
    }
}

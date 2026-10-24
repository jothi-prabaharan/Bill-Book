using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Foreign Currency Gain or Loss report (TK-220): its own endpoint, request and
/// answer, for its own page at <c>/reports/foreign-currency-gain-or-loss</c>, with one
/// lookup for its filter: the foreign currencies the branch holds a balance in at a
/// date, so the page can fetch a rate for each from the rate master. The report itself
/// never looks a rate up: the ones it is given are the ones it uses. The gateway routes
/// the exact paths to Reporting.
///
/// The reports module and <c>accounting.view</c>, on a branch with a trade. A request
/// naming another branch is refused with 403; a missing or reversed date, a currency
/// that is not a three-letter code or a rate that is not above nought is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/foreign-currency-gain-or-loss")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class ForeignCurrencyGainLossReportController : ControllerBase
{
    private readonly ForeignCurrencyGainLossReportRepository _report;

    public ForeignCurrencyGainLossReportController(ForeignCurrencyGainLossReportRepository report) => _report = report;

    /// <summary>What exchange rates have gained or lost on the branch's foreign-currency balances: realized for a period, unrealized at a date.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] ForeignCurrencyGainLossReportRequest request, CancellationToken ct)
    {
        if (IsAnotherBranch(request.OrgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in ForeignCurrencyGainLossReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's base currency and the foreign currencies it holds a balance in at a date.</summary>
    [HttpGet("currencies")]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Currencies([FromQuery] ForeignCurrencyChoicesRequest request, CancellationToken ct)
    {
        if (IsAnotherBranch(request.OrgId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.ChoicesAsync(request.AsAtDate!.Value, ct));
    }

    /// <summary>A request that names a branch must name the token's.</summary>
    private bool IsAnotherBranch(Guid? orgId) =>
        orgId is Guid named
        && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
             && tokenOrg == named);
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Account Transaction report (TK-218): its own endpoint, request and answer,
/// for its own page at <c>/reports/account-transaction</c>. The accounts, contacts,
/// tax rates and sources its filters offer are the chart, the contact list, the tax
/// master and Master's transaction types, which the other pages already read, so it
/// has no lookups of its own. The gateway routes the exact path to Reporting.
///
/// The reports module and <c>accounting.view</c>, on a branch with a trade. A
/// request naming another branch is refused with 403; a missing or reversed date, too
/// many accounts, or an account, contact or tax rate that is not the branch's is a
/// field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/account-transaction")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class AccountTransactionReportController : ControllerBase
{
    private readonly AccountTransactionReportRepository _report;

    public AccountTransactionReportController(AccountTransactionReportRepository report) => _report = report;

    /// <summary>A page of the period's postings, grouped by account, with the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] AccountTransactionRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in AccountTransactionReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        foreach ((string field, string message) in await _report.LookupRefusalsAsync(request, ct))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }
}

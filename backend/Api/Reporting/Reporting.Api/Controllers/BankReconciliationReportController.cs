using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Reconciliation Report (TK-202): its own endpoint, request and answer,
/// for its own page at <c>/reports/reconciliation-report</c>, and the bank
/// accounts it can be run for. The gateway routes both exact paths to
/// Reporting, as it does the other statements.
///
/// The same guards as the other financial statements: the reports module and
/// <c>accounting.view</c>, on a branch with a trade. The rows are the token's
/// branch's through the query filter: a request naming another branch is
/// refused with 403, and a bank account that is not the branch's is 404.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/bank-reconciliation")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class BankReconciliationReportController : ControllerBase
{
    private readonly BankReconciliationReportRepository _report;

    public BankReconciliationReportController(BankReconciliationReportRepository report) => _report = report;

    /// <summary>The reconciliation of one bank account at the end of one day.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] BankReconciliationReportRequest request, CancellationToken ct)
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

        BankReconciliationReportResultDto? result = await _report.RunAsync(request, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>The branch's bank accounts, for the page's account filter.</summary>
    [HttpGet("accounts")]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Accounts(CancellationToken ct) => Ok(await _report.AccountsAsync(ct));
}

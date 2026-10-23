using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Bank Summary (TK-219): its own endpoint, request and answer, for its own page
/// at <c>/reports/bank-summary</c>. The bank accounts its filter offers are the
/// Reconciliation Report's list (<c>api/statements/bank-reconciliation/accounts</c>),
/// so it has no lookup of its own. The gateway routes the exact path to Reporting.
///
/// The reports module and <c>accounting.view</c>, on a branch with a trade. A request
/// naming another branch is refused with 403; a missing or reversed date, or a bank
/// account that is not the branch's, is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/bank-summary")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class BankSummaryReportController : ControllerBase
{
    private readonly BankSummaryReportRepository _report;

    public BankSummaryReportController(BankSummaryReportRepository report) => _report = report;

    /// <summary>Each bank and cash account's opening balance, what came in and went out, and its closing balance for a period.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] BankSummaryReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in BankSummaryReportRepository.DateRefusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // The query filter decides which bank accounts are this branch's; another branch's is a refusal at the field.
        if (request.BankAccountId is long bankAccountId && !await _report.BankAccountExistsAsync(bankAccountId, ct))
        {
            ModelState.AddModelError(nameof(BankSummaryReportRequest.BankAccountId), "That bank account is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }
}

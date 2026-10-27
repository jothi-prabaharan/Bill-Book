using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Bill/DN Payment Report (TK-238): its own endpoint, request and answer, for
/// its own page at <c>/reports/bill-dn-payment-report</c>. The payments to vendors posted in a
/// period, newest first, each with the documents it settled and what it did not, and the totals of
/// all of them and of the vendors paid most. The vendors and bank accounts its filters
/// offer are under <c>…/vendors</c> and <c>…/bank-accounts</c>. The gateway routes the exact
/// paths to Reporting.
///
/// The reports module and <c>purchase.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an unknown payment method, or a
/// vendor or bank account that is not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/bill-dn-payment-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class BillDnPaymentReportController : ControllerBase
{
    private readonly BillDnPaymentReportRepository _report;

    public BillDnPaymentReportController(BillDnPaymentReportRepository report) => _report = report;

    /// <summary>A page of the period's payments with the documents each settled, and the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Run([FromQuery] BillDnPaymentReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in BillDnPaymentReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // The query filter decides which vendors and accounts are this branch's; another branch's is a refusal at the field.
        if (request.ContactId is long contactId && !await _report.ContactExistsAsync(contactId, ct))
        {
            ModelState.AddModelError(nameof(BillDnPaymentReportRequest.ContactId), "That vendor is not on this branch.");
        }

        if (request.BankAccountId is long bankAccountId && !await _report.BankAccountExistsAsync(bankAccountId, ct))
        {
            ModelState.AddModelError(nameof(BillDnPaymentReportRequest.BankAccountId), "That bank account is not on this branch.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's vendors, for the filter.</summary>
    [HttpGet("vendors")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Vendors(CancellationToken ct) => Ok(await _report.VendorsAsync(ct));

    /// <summary>The branch's bank accounts, for the filter.</summary>
    [HttpGet("bank-accounts")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> BankAccounts(CancellationToken ct) => Ok(await _report.BankAccountsAsync(ct));
}

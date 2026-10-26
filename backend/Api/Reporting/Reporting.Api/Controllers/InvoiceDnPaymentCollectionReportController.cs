using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Invoice/DN Payment Collection Report (TK-227): its own endpoint, request and answer, for
/// its own page at <c>/reports/invoice-dn-payment-collection-report</c>. The receipts posted in a
/// period, newest first, each with the documents it settled and what it did not, and the totals of
/// all of them and of the customers who paid most. The customers and bank accounts its filters
/// offer are under <c>…/customers</c> and <c>…/bank-accounts</c>. The gateway routes the exact
/// paths to Reporting.
///
/// The reports module and <c>sales.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an unknown payment method, or a
/// customer or bank account that is not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/invoice-dn-payment-collection-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class InvoiceDnPaymentCollectionReportController : ControllerBase
{
    private readonly InvoiceDnPaymentCollectionReportRepository _report;

    public InvoiceDnPaymentCollectionReportController(InvoiceDnPaymentCollectionReportRepository report) => _report = report;

    /// <summary>A page of the period's receipts with the documents each settled, and the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Run([FromQuery] InvoiceDnPaymentCollectionReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in InvoiceDnPaymentCollectionReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // The query filter decides which customers and accounts are this branch's; another branch's is a refusal at the field.
        if (request.ContactId is long contactId && !await _report.ContactExistsAsync(contactId, ct))
        {
            ModelState.AddModelError(nameof(InvoiceDnPaymentCollectionReportRequest.ContactId), "That customer is not on this branch.");
        }

        if (request.BankAccountId is long bankAccountId && !await _report.BankAccountExistsAsync(bankAccountId, ct))
        {
            ModelState.AddModelError(nameof(InvoiceDnPaymentCollectionReportRequest.BankAccountId), "That bank account is not on this branch.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's customers, for the filter.</summary>
    [HttpGet("customers")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Customers(CancellationToken ct) => Ok(await _report.CustomersAsync(ct));

    /// <summary>The branch's bank accounts, for the filter.</summary>
    [HttpGet("bank-accounts")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> BankAccounts(CancellationToken ct) => Ok(await _report.BankAccountsAsync(ct));
}

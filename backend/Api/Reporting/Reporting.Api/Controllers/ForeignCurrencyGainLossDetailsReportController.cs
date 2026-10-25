using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Foreign Currency Gain or Loss Details report (TK-221): its own endpoint, request and
/// answer, for its own page at <c>/reports/foreign-currency-gain-or-loss-details</c>, with
/// one lookup for its filters: the currencies, accounts and contacts with a foreign-currency
/// posting up to a date. Like the summary it never looks a rate up: the ones it is given are
/// the ones it uses. The gateway routes the exact paths to Reporting.
///
/// The reports module and <c>accounting.view</c>, on a branch with a trade. A request naming
/// another branch is refused with 403; a missing or reversed date, a currency that is not a
/// three-letter code, a rate that is not above nought, and a contact or account that is not
/// the branch's are field errors.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/foreign-currency-gain-or-loss-details")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class ForeignCurrencyGainLossDetailsReportController : ControllerBase
{
    private readonly ForeignCurrencyGainLossDetailsReportRepository _report;

    public ForeignCurrencyGainLossDetailsReportController(ForeignCurrencyGainLossDetailsReportRepository report) => _report = report;

    /// <summary>The gains and losses of the Foreign Currency Gain or Loss report, document by document.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] ForeignCurrencyGainLossDetailsRequest request, CancellationToken ct)
    {
        if (IsAnotherBranch(request.OrgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in ForeignCurrencyGainLossDetailsReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // The query filter decides what is this branch's; another branch's reads as none, and is refused at its field.
        if (request.ContactId is long contactId && !await _report.ContactExistsAsync(contactId, ct))
        {
            ModelState.AddModelError(nameof(ForeignCurrencyGainLossDetailsRequest.ContactId), "That contact is not on this branch.");
        }

        if (request.AccountId is long accountId && !await _report.AccountExistsAsync(accountId, ct))
        {
            ModelState.AddModelError(nameof(ForeignCurrencyGainLossDetailsRequest.AccountId), "That account is not on this branch.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The base currency, and the currencies, accounts and contacts with a foreign-currency posting up to a date.</summary>
    [HttpGet("choices")]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Choices([FromQuery] ForeignCurrencyDetailChoicesRequest request, CancellationToken ct)
    {
        if (IsAnotherBranch(request.OrgId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.ChoicesAsync(request.ToDate!.Value, ct));
    }

    /// <summary>A request that names a branch must name the token's.</summary>
    private bool IsAnotherBranch(Guid? orgId) =>
        orgId is Guid named
        && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
             && tokenOrg == named);
}

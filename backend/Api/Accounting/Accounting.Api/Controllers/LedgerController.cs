using System.Security.Claims;
using Accounting.Api.Services;
using Accounting.Repository.Repositories;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Accounting.Api.Controllers;

/// <summary>
/// Accounting › Ledger. The two reads that make every posting in the product
/// checkable by a person rather than by a database client.
///
/// Separate from <c>InternalLedgerController</c> on purpose: that one is the
/// write door, keyed and open to other services; this one is a user-facing read
/// behind a token and <c>accounting.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/ledger")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.School, BusinessType.Service)]
public sealed class LedgerController : ControllerBase
{
    private readonly LedgerReportRepository _reports;
    private readonly TrialBalanceReportRepository _trialBalance;

    public LedgerController(LedgerReportRepository reports, TrialBalanceReportRepository trialBalance)
    {
        _reports = reports;
        _trialBalance = trialBalance;
    }

    [HttpGet("accounts/{accountId:long}")]
    public async Task<IActionResult> AccountLedger(
        long accountId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        AccountLedgerView? ledger = await _reports.GetAccountLedgerAsync(accountId, from, to, ct);

        // NotFound rather than an empty ledger: the query filter is what decided
        // the account is not here, and an account in another branch and an
        // account that never existed should be indistinguishable from outside.
        return ledger is null ? NotFound() : Ok(ledger);
    }

    /// <summary>
    /// The Trial Balance report (TK-215): a period with both ends required, each
    /// account's opening balance, movement and closing balance, a subtotal for
    /// each account type and the totals that must agree, with a second period
    /// beside it when asked. A request naming another branch is 403, a missing or
    /// reversed date a field error.
    /// </summary>
    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance(
        [FromQuery] TrialBalanceReportRequest request, CancellationToken ct)
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

        IReadOnlyList<(string Field, string Message)> refusals = TrialBalanceReportRepository.DateRefusals(request);
        if (refusals.Count > 0)
        {
            foreach ((string field, string message) in refusals)
            {
                ModelState.AddModelError(field, message);
            }

            return ValidationProblem(ModelState);
        }

        return Ok(await _trialBalance.RunAsync(request, ct));
    }

    /// <summary>
    /// Each account's debit or credit balance over an optional period: the feed
    /// of the chart of accounts, which shows a balance beside every account and
    /// has no use for a report's opening balance, subtotals or required dates.
    /// It was <c>trial-balance</c> until the report took that address.
    /// </summary>
    [HttpGet("account-balances")]
    public async Task<IActionResult> AccountBalances(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _reports.GetTrialBalanceAsync(from, to, ct));

    /// <summary>
    /// Every control account against the subledger beneath it — the check
    /// double-entry cannot make, because a receivable posted with no sub-account
    /// balances perfectly while nobody owes the money.
    /// </summary>
    [HttpGet("sub-ledger-tie")]
    public async Task<IActionResult> SubLedgerTie(
        [FromQuery] DateOnly? asAt, CancellationToken ct) =>
        Ok(await _reports.GetSubLedgerTieAsync(asAt, ct));

    /// <summary>
    /// What a document was booked at. Banking reads it before settling one in a
    /// foreign currency, so the balance comes off at the rate it went on at and
    /// the difference lands in Realized FX Gain/Loss rather than as a residue on
    /// the contact.
    ///
    /// On the token rather than behind the internal key, because the caller is
    /// always acting for a signed-in user settling a document — and the tenant
    /// then comes from the same claims that decided which books they are in.
    /// </summary>
    [HttpGet("documents/{transactionTypeCode}/{transactionId:long}/rate")]
    public async Task<IActionResult> SettlementRate(
        string transactionTypeCode, long transactionId, CancellationToken ct)
    {
        SettlementRateView? rate =
            await _reports.GetSettlementRateAsync(transactionTypeCode, transactionId, ct);

        // NotFound covers both "never posted" and "not in this branch", and that
        // is the point: the query filter made the decision, and a caller must not
        // be able to tell a document in another branch from one that never was.
        return rate is null ? NotFound() : Ok(rate);
    }

    [HttpGet("contacts/{contactId:long}/outstanding-balances/{ledgerTypeId:int}")]
    public async Task<IActionResult> OutstandingBalances(
        long contactId, int ledgerTypeId, CancellationToken ct) =>
        Ok(await _reports.GetOutstandingBalancesAsync(contactId, ledgerTypeId, ct));

    [HttpGet("outstanding-balances/{ledgerTypeId:int}")]
    public async Task<IActionResult> AllOutstandingBalances(int ledgerTypeId, CancellationToken ct) =>
        Ok(await _reports.GetAllOutstandingBalancesAsync(ledgerTypeId, ct));
}

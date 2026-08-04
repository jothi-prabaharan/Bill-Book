using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

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
public sealed class LedgerController : ControllerBase
{
    private readonly LedgerReportService _reports;

    public LedgerController(LedgerReportService reports) => _reports = reports;

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

    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance(
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
}

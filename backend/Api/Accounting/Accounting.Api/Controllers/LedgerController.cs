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
}

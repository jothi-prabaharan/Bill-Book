using Banking.Api.Services;
using Banking.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Ordering;

namespace Banking.Api.Controllers;

/// <summary>
/// Banking › Bank accounts. Creating one creates its GL account in Accounting,
/// because only Accounting writes ledger rows and a bank account without one
/// cannot be posted to.
/// </summary>
[ApiController]
[Authorize]
[Route("api/bank-accounts")]
public sealed class BankAccountsController : ControllerBase
{
    private readonly BankService _banks;

    public BankAccountsController(BankService banks) => _banks = banks;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _banks.ListAccountsAsync(includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveBankAccountRequest request, CancellationToken ct) =>
        BankingResponses.From(this, await _banks.CreateAccountAsync(request, ct),
            () => StatusCode(StatusCodes.Status201Created));

    [HttpPut("{bankAccountId:long}")]
    public async Task<IActionResult> Update(
        long bankAccountId, [FromBody] SaveBankAccountRequest request, CancellationToken ct) =>
        BankingResponses.From(this, await _banks.UpdateAccountAsync(bankAccountId, request, ct), NoContent);

    [HttpPut("{bankAccountId:long}/default")]
    public async Task<IActionResult> SetDefault(long bankAccountId, CancellationToken ct) =>
        BankingResponses.From(this, await _banks.SetDefaultAccountAsync(bankAccountId, ct), NoContent);

    /// <summary>Links an account whose ledger call failed when it was created.</summary>
    [HttpPost("{bankAccountId:long}/link-ledger")]
    public async Task<IActionResult> LinkLedger(long bankAccountId, CancellationToken ct) =>
        BankingResponses.From(this, await _banks.RetryLedgerLinkAsync(bankAccountId, ct), NoContent);

    [HttpDelete("{bankAccountId:long}")]
    public async Task<IActionResult> Deactivate(long bankAccountId, CancellationToken ct) =>
        BankingResponses.From(this, await _banks.DeactivateAccountAsync(bankAccountId, ct), NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken ct) =>
        BankingResponses.From(this, await _banks.ReorderAccountsAsync(request, ct), NoContent);
}

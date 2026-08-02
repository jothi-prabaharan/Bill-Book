using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// Called by Banking when a bank account is created or renamed. Only Accounting
/// writes ledger rows, so the GL account behind a bank account is made here
/// rather than inserted from the other side.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/accounts/bank-account")]
public sealed class InternalBankAccountsController : ControllerBase
{
    private readonly BankLedgerService _ledger;

    public InternalBankAccountsController(BankLedgerService ledger) => _ledger = ledger;

    [HttpPost]
    public async Task<IActionResult> Provision(
        [FromBody] ProvisionBankAccountRequest request, CancellationToken ct)
    {
        BankLedgerResult result = await _ledger.ProvisionAsync(request, ct);

        return result.Outcome switch
        {
            BankLedgerOutcome.Ok => Ok(new
            {
                accountId = result.AccountId,
                accountCode = result.AccountCode,
            }),
            BankLedgerOutcome.ParentMissing => StatusCode(StatusCodes.Status409Conflict, new MessageResponse
            {
                Message = "The chart of accounts has no bank parent group, so the account could "
                    + "not be created. The organization's chart of accounts needs reseeding.",
            }),
            BankLedgerOutcome.InvalidValue => BadRequest(new MessageResponse
            {
                Message = "That account type is not recognised.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPut("{bankAccountId:long}")]
    public async Task<IActionResult> Update(
        long bankAccountId, [FromBody] UpdateBankAccountLedgerRequest request, CancellationToken ct)
    {
        BankLedgerOutcome outcome = await _ledger.UpdateAsync(
            bankAccountId, request.AccountName, request.IsActive, ct);

        return outcome == BankLedgerOutcome.Ok ? NoContent() : NotFound();
    }
}

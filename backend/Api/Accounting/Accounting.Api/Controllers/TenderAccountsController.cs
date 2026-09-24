using Accounting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Accounting.Api.Controllers;

/// <summary>
/// The accounts a till can take money into (TK-39), for the POS screen's tender
/// buttons.
///
/// <b>Guarded by <c>sales.view</c>, not <c>banking.view</c></b>: a cashier holds
/// the first and not the second, and what this returns — a name and a kind per
/// account, no number and no balance — is what a till needs and nothing a
/// banking screen protects. Posting still checks each account, in Accounting,
/// when the sale is posted.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/bank-accounts/tender-options")]
[RequireApp(App.RetailErp)]
public sealed class TenderAccountsController : ControllerBase
{
    private readonly BankService _banks;

    public TenderAccountsController(BankService banks) => _banks = banks;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _banks.ListTenderAccountsAsync(ct));
}

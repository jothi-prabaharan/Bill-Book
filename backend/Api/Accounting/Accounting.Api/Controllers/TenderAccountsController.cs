using Accounting.Api.Services;
using Accounting.Repository.Repositories;
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
/// the first and not the second, and what this returns â€” a name and a kind per
/// account, no number and no balance â€” is what a till needs and nothing a
/// banking screen protects. Posting still checks each account, in Accounting,
/// when the sale is posted.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/bank-accounts/tender-options")]
[RequireBusinessType(BusinessType.General, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.School)]
public sealed class TenderAccountsController : ControllerBase
{
    private readonly BankRepository _banks;

    public TenderAccountsController(BankRepository banks) => _banks = banks;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _banks.ListTenderAccountsAsync(ct));
}

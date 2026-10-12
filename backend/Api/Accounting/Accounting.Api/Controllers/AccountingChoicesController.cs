using Accounting.Entity.Enums;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// What the manual journal's create page offers (TK-192): the accounts a journal
/// line may take and the journal's next number. Behind <c>accounting.view</c>,
/// like the rest of the journal. See <see cref="DocumentChoicesRepository"/>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/choices/accounting")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.School, BusinessType.Service)]
public sealed class AccountingChoicesController : ControllerBase
{
    private static readonly string[] Series = ["JRN"];
    private readonly DocumentChoicesRepository _choices;

    public AccountingChoicesController(DocumentChoicesRepository choices) => _choices = choices;

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts(CancellationToken ct) =>
        Ok(await _choices.AccountsAsync(ChoiceUsage.Any, ct));

    /// <summary>
    /// The next number in one of this module's own series. Any other code is not
    /// found here: another module's numbering is that module's to show.
    /// </summary>
    [HttpGet("next-number/{seriesCode}")]
    public async Task<IActionResult> NextNumber(
        string seriesCode, [FromQuery] DateOnly? on, CancellationToken ct)
    {
        string code = seriesCode.Trim().ToUpperInvariant();
        return Series.Contains(code) ? Ok(await _choices.NextNumberAsync(code, on, ct)) : NotFound();
    }
}

using Accounting.Entity.Enums;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// What the create pages for receive, spend and transfer money offer (TK-192):
/// read-only option lists, behind <c>banking.view</c>. The data is Accounting's,
/// but the question is the banking module's — what may this use — so the authority
/// is the module's own, and a banking user picks from these without being able to
/// read the accounting masters. See <see cref="DocumentChoicesRepository"/>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("banking")]
[Route("api/choices/banking")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.School, BusinessType.Service)]
public sealed class BankingChoicesController : ControllerBase
{
    private static readonly string[] Series = ["RCM", "SPM", "TRM"];
    private readonly DocumentChoicesRepository _choices;

    public BankingChoicesController(DocumentChoicesRepository choices) => _choices = choices;

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts(CancellationToken ct) =>
        Ok(await _choices.AccountsAsync(ChoiceUsage.Payment, ct));

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

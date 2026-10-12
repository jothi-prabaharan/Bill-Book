using Accounting.Entity.Enums;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// What the create pages for purchase orders, goods receipts, bills and debit notes
/// offer (TK-192): read-only option lists, behind <c>purchase.view</c>. The data is
/// Accounting's, but the question is the purchase module's — what may this use — so
/// the authority is the module's own, and a purchase user picks from these without
/// being able to read the accounting masters.
/// See <see cref="DocumentChoicesRepository"/>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/choices/purchase")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.School, BusinessType.Service)]
public sealed class PurchaseChoicesController : ControllerBase
{
    private static readonly string[] Series = ["POR", "GRN", "BIL", "DBN"];
    private readonly DocumentChoicesRepository _choices;

    public PurchaseChoicesController(DocumentChoicesRepository choices) => _choices = choices;

    /// <summary>Tax rates in force on <paramref name="on"/> (today when omitted).</summary>
    [HttpGet("tax-rates")]
    public async Task<IActionResult> TaxRates([FromQuery] DateOnly? on, CancellationToken ct) =>
        Ok(await _choices.TaxRatesAsync(ChoiceUsage.Purchase, on, ct));

    [HttpGet("payment-terms")]
    public async Task<IActionResult> PaymentTerms(CancellationToken ct) =>
        Ok(await _choices.PaymentTermsAsync(ChoiceUsage.Purchase, ct));

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts(CancellationToken ct) =>
        Ok(await _choices.AccountsAsync(ChoiceUsage.Purchase, ct));

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

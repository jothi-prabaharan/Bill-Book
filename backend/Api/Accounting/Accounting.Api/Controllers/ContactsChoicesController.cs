using Accounting.Entity.Enums;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// The payment terms and accounts the contact form offers for a contact's trading
/// terms (TK-192): read-only option lists, behind <c>contacts.view</c>. The data is
/// Accounting's, but the question is the contacts module's — what may this use — so
/// the authority is the module's own, and a contacts user picks from these without
/// being able to read the accounting masters.
/// See <see cref="DocumentChoicesRepository"/>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("contacts")]
[Route("api/choices/contacts")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.School, BusinessType.Service)]
public sealed class ContactsChoicesController : ControllerBase
{
    private readonly DocumentChoicesRepository _choices;

    public ContactsChoicesController(DocumentChoicesRepository choices) => _choices = choices;

    [HttpGet("payment-terms")]
    public async Task<IActionResult> PaymentTerms(CancellationToken ct) =>
        Ok(await _choices.PaymentTermsAsync(ChoiceUsage.Any, ct));

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts(CancellationToken ct) =>
        Ok(await _choices.AccountsAsync(ChoiceUsage.Any, ct));
}

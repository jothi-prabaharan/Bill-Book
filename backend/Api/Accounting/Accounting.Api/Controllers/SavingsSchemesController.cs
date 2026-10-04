using System.Data;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;

namespace Accounting.Api.Controllers;

/// <summary>
/// Jewellery savings schemes (TK-141): a paid add-on for Jewellery branches.
///
/// Under the <c>sales</c> module, because the counter takes instalments: taking
/// one needs <c>sales.create</c>; redeeming and refunding move money already
/// held and need <c>sales.approve</c>, the permission that posts an invoice.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[RequireBusinessType(BusinessType.Jewellery)]
[RequireAddOn(AddOn.SavingsScheme)]
[Route("api/savings-schemes")]
public sealed class SavingsSchemesController : ControllerBase
{
    private readonly SavingsSchemeRepository _schemes;

    public SavingsSchemesController(SavingsSchemeRepository schemes) => _schemes = schemes;

    [HttpGet]
    public async Task<IActionResult> ListSchemes(CancellationToken ct) => Ok(await _schemes.ListSchemesAsync(ct));

    [HttpPost]
    public async Task<IActionResult> CreateScheme([FromBody] SaveSavingsSchemeRequest request, CancellationToken ct) =>
        Map(await _schemes.SaveSchemeAsync(null, request, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateScheme(long id, [FromBody] SaveSavingsSchemeRequest request, CancellationToken ct) =>
        Map(await _schemes.SaveSchemeAsync(id, request, ct));

    [HttpGet("bank-accounts")]
    public async Task<IActionResult> BankAccounts(CancellationToken ct) => Ok(await _schemes.BankAccountsAsync(ct));

    [HttpGet("enrolments/{id:long}/invoices")]
    public async Task<IActionResult> OpenInvoices(long id, CancellationToken ct) => Ok(await _schemes.OpenInvoicesAsync(id, ct));

    [HttpGet("enrolments")]
    public async Task<IActionResult> ListEnrolments(
        [FromQuery] EnrolmentStatus? status, [FromQuery] long? contactId, CancellationToken ct) =>
        Ok(await _schemes.ListEnrolmentsAsync(status, contactId, ct));

    [HttpGet("enrolments/{id:long}")]
    public async Task<IActionResult> GetEnrolment(long id, CancellationToken ct) =>
        await _schemes.GetEnrolmentAsync(id, ct) is SchemeEnrolmentView view ? Ok(view) : NotFound();

    [HttpPost("enrolments")]
    public async Task<IActionResult> Enrol([FromBody] EnrolRequest request, CancellationToken ct) =>
        Map(await _schemes.EnrolAsync(request, ct));

    /// <summary>Takes the next unpaid instalment. 200 with the receipt's id.</summary>
    [HttpPost("enrolments/{id:long}/instalments")]
    [PermissionAction("create")]
    public async Task<IActionResult> PayInstalment(long id, [FromBody] PayInstalmentRequest request, CancellationToken ct) =>
        Map(await _schemes.PayInstalmentAsync(id, request, ct));

    [HttpPost("enrolments/{id:long}/redeem")]
    [PermissionAction("approve")]
    [Transactional(IsolationLevel.Serializable)]
    public async Task<IActionResult> Redeem(long id, [FromBody] RedeemEnrolmentRequest request, CancellationToken ct) =>
        Map(await _schemes.RedeemAsync(id, request, ct));

    /// <summary>200 with the refund's spend-money id.</summary>
    [HttpPost("enrolments/{id:long}/refund")]
    [PermissionAction("approve")]
    [Transactional(IsolationLevel.Serializable)]
    public async Task<IActionResult> Refund(long id, [FromBody] RefundEnrolmentRequest request, CancellationToken ct) =>
        Map(await _schemes.RefundAsync(id, request, ct));

    private IActionResult Map(SavingsSchemeResult result) => result.Outcome switch
    {
        SavingsSchemeOutcome.Ok => Ok(new { id = result.Id }),
        SavingsSchemeOutcome.NotFound => NotFound(),
        SavingsSchemeOutcome.Conflict or SavingsSchemeOutcome.Retry =>
            Conflict(new MessageResponse { Message = result.Detail ?? "That cannot be done now." }),
        SavingsSchemeOutcome.PostingRefused =>
            UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "The posting was refused." }),
        _ => BadRequest(new MessageResponse { Message = result.Detail ?? "The request was refused." }),
    };
}

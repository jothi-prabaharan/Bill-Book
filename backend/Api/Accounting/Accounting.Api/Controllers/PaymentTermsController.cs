using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Ordering;

namespace Accounting.Api.Controllers;

/// <summary>
/// Settings › Payment terms. Owned by Accounting because Sales and Purchase
/// both need it, so it cannot live inside either — the same reasoning as Tax
/// Master and Numbering series.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/payment-terms")]
public sealed class PaymentTermsController : ControllerBase
{
    private readonly PaymentTermService _terms;

    public PaymentTermsController(PaymentTermService terms) => _terms = terms;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _terms.ListAsync(includeInactive, ct));

    [HttpGet("{paymentTermId:long}")]
    public async Task<IActionResult> Get(long paymentTermId, CancellationToken ct)
    {
        PaymentTermListItem? row = await _terms.GetAsync(paymentTermId, ct);
        return row is null ? NotFound() : Ok(row);
    }

    /// <summary>
    /// The due date a document dated <paramref name="documentDate"/> gets under
    /// this term. Sales and Purchase call this instead of carrying their own copy
    /// of the rules — two implementations would disagree about End of Month.
    /// </summary>
    [HttpGet("{paymentTermId:long}/due-date")]
    public async Task<IActionResult> DueDate(
        long paymentTermId, [FromQuery] DateOnly documentDate, CancellationToken ct)
    {
        DueDateResult? result = await _terms.ResolveDueDateAsync(paymentTermId, documentDate, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SavePaymentTermRequest request, CancellationToken ct)
    {
        SaveTermResult result = await _terms.CreateAsync(request, ct);
        return Respond(result.Outcome, () => CreatedAtAction(
            nameof(Get),
            new { paymentTermId = result.PaymentTermId },
            new { paymentTermId = result.PaymentTermId }));
    }

    [HttpPut("{paymentTermId:long}")]
    public async Task<IActionResult> Update(
        long paymentTermId, [FromBody] SavePaymentTermRequest request, CancellationToken ct)
    {
        SaveTermResult result = await _terms.UpdateAsync(paymentTermId, request, ct);
        return Respond(result.Outcome, NoContent);
    }

    [HttpPut("{paymentTermId:long}/default")]
    public async Task<IActionResult> SetDefault(long paymentTermId, CancellationToken ct) =>
        Respond(await _terms.SetDefaultAsync(paymentTermId, ct), NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken ct) =>
        Respond(await _terms.ReorderAsync(request, ct), NoContent);

    [HttpDelete("{paymentTermId:long}")]
    public async Task<IActionResult> Deactivate(long paymentTermId, CancellationToken ct) =>
        Respond(await _terms.DeactivateAsync(paymentTermId, ct), NoContent);

    private IActionResult Respond(SaveTermOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            SaveTermOutcome.Ok => onOk(),
            SaveTermOutcome.NotFound => NotFound(),
            SaveTermOutcome.DuplicateName => BadRequest(new MessageResponse
            {
                Message = "Another payment term already uses that name.",
            }),
            SaveTermOutcome.NoApplicability => BadRequest(new MessageResponse
            {
                Message = "A term must apply to sales, purchases, or both.",
            }),
            SaveTermOutcome.DayOfMonthRequired => BadRequest(new MessageResponse
            {
                Message = "Choose which day of the following month payment falls due.",
            }),
            SaveTermOutcome.DiscountWindowTooLong => BadRequest(new MessageResponse
            {
                Message = "The discount period cannot run past the due date — it could never be earned.",
            }),
            SaveTermOutcome.DiscountDaysRequired => BadRequest(new MessageResponse
            {
                Message = "A discount needs a number of days to be paid within.",
            }),
            SaveTermOutcome.SystemTermLocked => BadRequest(new MessageResponse
            {
                Message = "A built-in term's rule cannot be changed — contacts and unpaid documents "
                    + "already use it, and their due dates would move. Rename it, or add a new term.",
            }),
            SaveTermOutcome.InvalidValue => BadRequest(new MessageResponse
            {
                Message = "One of the selected options is not a recognised value.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}

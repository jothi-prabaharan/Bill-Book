using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services.Pdf;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Sales.Api.Controllers;

/// <summary>
/// Sales › Credit notes. <c>CRN</c> — the correction of a posted invoice.
///
/// <b>Posting is an approval and voiding is its own permission</b>, the same
/// separation every other sales document has: <c>sales.approve</c> to post,
/// <c>sales.void</c> to withdraw. Both routes used to fall back to the
/// action the HTTP method implies, so anyone who could edit a draft could
/// post it or void a posted one.
///
/// <b>Every refusal is an outcome.</b> A credit note outside the caller's branch
/// is <c>NotFound</c>, like an id that is nobody's (TK-71); a rule about the
/// document comes back with the service's own sentence.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/credit-notes")]
[RequireApp(App.RetailErp)]
public sealed class CreditNotesController : ControllerBase
{
    private readonly CreditNoteService _service;

    public CreditNotesController(CreditNoteService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var list = await _service.ListAsync(from, to, ct);
        return Ok(list);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var view = await _service.GetAsync(id, ct);
        return view is null ? NotFound() : Ok(view);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveCreditNoteRequest request, CancellationToken ct)
    {
        CreditNoteResult result = await _service.SaveAsync(null, request, ct);

        return result.Outcome == CreditNoteOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { id = result.CreditNoteId }, result)
            : Respond(result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveCreditNoteRequest request, CancellationToken ct)
    {
        CreditNoteResult result = await _service.SaveAsync(id, request, ct);
        return result.Outcome == CreditNoteOutcome.Ok ? Ok(result) : Respond(result);
    }

    /// <summary>Claims the note against its invoice, returns any goods, and reverses the revenue and tax.</summary>
    [HttpPost("{id:long}/post")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Post(long id, CancellationToken ct)
    {
        CreditNoteResult result = await _service.PostAsync(id, ct);
        return result.Outcome == CreditNoteOutcome.Ok ? Ok(result) : Respond(result);
    }

    /// <summary>
    /// Withdraws a credit note. The reason is required by the database, not just
    /// by convention — see <see cref="VoidCreditNoteRequest"/>.
    /// </summary>
    [HttpPost("{id:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(long id, [FromBody] VoidCreditNoteRequest request, CancellationToken ct)
    {
        CreditNoteResult result = await _service.VoidAsync(id, request.Reason, ct, request.CancelReason);
        return result.Outcome == CreditNoteOutcome.Ok ? Ok(result) : Respond(result);
    }

    /// <summary>The credit note's registration at the IRP (TK-92). Not found when it has none.</summary>
    [HttpGet("{id:long}/e-invoice")]
    [PermissionAction("view")]
    public async Task<IActionResult> EInvoice(
        long id, [FromServices] Sales.Api.Services.EInvoicing.EInvoiceActions actions, CancellationToken ct)
    {
        EInvoiceStateView? state = await actions.GetAsync(Sales.Entity.Enums.EInvoiceSource.CreditNote, id, ct);
        return state is null ? NotFound() : Ok(state);
    }

    /// <summary>
    /// Registers the credit note at the IRP again now (TK-92): after a refusal was
    /// fixed, or instead of waiting for the retry worker. Needs sales.einvoice.
    /// </summary>
    [HttpPost("{id:long}/e-invoice/retry")]
    [PermissionAction("einvoice")]
    public async Task<IActionResult> RetryEInvoice(
        long id, [FromServices] Sales.Api.Services.EInvoicing.EInvoiceActions actions, CancellationToken ct)
    {
        EInvoiceStateView? state = await actions.RetryAsync(Sales.Entity.Enums.EInvoiceSource.CreditNote, id, ct);
        return state is null ? NotFound() : Ok(state);
    }

    private IActionResult Respond(CreditNoteResult result) =>
        result.Outcome switch
        {
            CreditNoteOutcome.NotFound => NotFound(),
            CreditNoteOutcome.LifecycleRefused or CreditNoteOutcome.OverReturned
                or CreditNoteOutcome.AllocationRefused or CreditNoteOutcome.StockRefused
                or CreditNoteOutcome.PostingRefused or CreditNoteOutcome.EInvoiceRefused => Conflict(Message(result)),
            CreditNoteOutcome.RatesUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable, Message(result)),
            _ => UnprocessableEntity(Message(result)),
        };

    private static MessageResponse Message(CreditNoteResult result) =>
        new() { Message = result.Detail ?? "This credit note was refused." };

    /// <summary>
    /// The PDF archived when this credit note was posted (TK-22). Not found for a
    /// draft, for a credit note in another branch, or when the file is missing — the
    /// three are not told apart. Needs <c>sales.print</c>, as printing does: both
    /// hand the document to someone outside the business.
    /// </summary>
    [HttpGet("{id:long}/pdf")]
    [PermissionAction("print")]
    public async Task<IActionResult> DownloadPdf(
        long id, [FromServices] SalesDocumentArchive archive, CancellationToken ct)
    {
        ArchivedPdf? pdf = await archive.OpenAsync(ArchivedSalesDocument.CreditNote, id, ct);
        return pdf is null ? NotFound() : File(pdf.Content, "application/pdf", pdf.FileName);
    }
}

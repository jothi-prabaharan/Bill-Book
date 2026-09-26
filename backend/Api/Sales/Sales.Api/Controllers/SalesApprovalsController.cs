using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.TableEntities;
using Shared.Kernel.Approvals;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// Sales approval chains (TK-102): a credit note's own chain, and the
/// credit-limit and discount overrides on an invoice or sales order.
///
/// <b>Submitting a credit note is an edit</b> (<c>sales.edit</c>). An override
/// is asked for by saving the document with <c>requestApproval</c>, because the
/// save is what finds the breach, so it has no submit route of its own.
/// <b>Acting and reading need only <c>sales.view</c></b>: being the level's
/// approver is the authority, and the chain refuses anyone else.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales")]
[RequireApp(App.RetailErp)]
public sealed class SalesApprovalsController : ControllerBase
{
    private const string Overrides = "{kind:regex(^(credit-limit|discount)$)}";

    private readonly CreditNoteApprovalService _creditNotes;
    private readonly InvoiceOverrideService _invoices;
    private readonly SalesOrderOverrideService _orders;

    public SalesApprovalsController(
        CreditNoteApprovalService creditNotes, InvoiceOverrideService invoices, SalesOrderOverrideService orders)
    {
        _creditNotes = creditNotes;
        _invoices = invoices;
        _orders = orders;
    }

    [HttpPost("credit-notes/{id:long}/submit")]
    [PermissionAction("edit")]
    public async Task<IActionResult> SubmitCreditNote(long id, CancellationToken ct) =>
        Respond(await _creditNotes.SubmitAsync(ApprovalRequestKind.CreditNote, id, ct));

    [HttpGet("credit-notes/{id:long}/approval")]
    [PermissionAction("view")]
    public async Task<IActionResult> CreditNoteChain(long id, CancellationToken ct) =>
        await _creditNotes.ChainAsync(ApprovalRequestKind.CreditNote, id, ct) is { } chain ? Ok(chain) : NotFound();

    [HttpPost("credit-notes/{id:long}/approval")]
    [PermissionAction("view")]
    public async Task<IActionResult> ActOnCreditNote(long id, [FromBody] DocumentApprovalActionRequest request, CancellationToken ct) =>
        Respond(await _creditNotes.ActAsync(ApprovalRequestKind.CreditNote, id, request.Action, request.Comments, ct));

    [HttpGet("invoices/{id:long}/overrides/" + Overrides + "/approval")]
    [PermissionAction("view")]
    public async Task<IActionResult> InvoiceOverride(long id, string kind, CancellationToken ct) =>
        await _invoices.ChainAsync(OverrideKind(kind), id, ct) is { } chain ? Ok(chain) : NotFound();

    [HttpPost("invoices/{id:long}/overrides/" + Overrides + "/approval")]
    [PermissionAction("view")]
    public async Task<IActionResult> ActOnInvoiceOverride(
        long id, string kind, [FromBody] DocumentApprovalActionRequest request, CancellationToken ct) =>
        Respond(await _invoices.ActAsync(OverrideKind(kind), id, request.Action, request.Comments, ct));

    [HttpGet("sales-orders/{id:long}/overrides/" + Overrides + "/approval")]
    [PermissionAction("view")]
    public async Task<IActionResult> OrderOverride(long id, string kind, CancellationToken ct) =>
        await _orders.ChainAsync(OverrideKind(kind), id, ct) is { } chain ? Ok(chain) : NotFound();

    [HttpPost("sales-orders/{id:long}/overrides/" + Overrides + "/approval")]
    [PermissionAction("view")]
    public async Task<IActionResult> ActOnOrderOverride(
        long id, string kind, [FromBody] DocumentApprovalActionRequest request, CancellationToken ct) =>
        Respond(await _orders.ActAsync(OverrideKind(kind), id, request.Action, request.Comments, ct));

    /// <summary>
    /// Everything in Sales waiting on the signed-in user: credit notes and
    /// overrides alike. The approvals inbox (TK-103) asks each service.
    /// </summary>
    [HttpGet("approvals/mine")]
    [PermissionAction("view")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        List<ApprovalInboxItem> items =
        [
            .. await _creditNotes.MineAsync(ct),
            .. await _invoices.MineAsync(ct),
            .. await _orders.MineAsync(ct),
        ];

        return Ok(items.OrderBy(i => i.DocumentDate));
    }

    private static ApprovalRequestKind OverrideKind(string kind) => SalesOverrideService<Invoice>.KindOf(kind)!.Value;

    private IActionResult Respond(ApprovalResult result) => result.Outcome switch
    {
        ApprovalResultOutcome.Ok => Ok(new { approvalStatus = result.ApprovalStatus }),
        ApprovalResultOutcome.NotFound => NotFound(),
        ApprovalResultOutcome.NoWorkflow => Ok(new { approvalStatus = (string?)null, message = result.Detail }),
        ApprovalResultOutcome.NotTheApprover => StatusCode(StatusCodes.Status403Forbidden, new MessageResponse { Message = result.Detail! }),
        ApprovalResultOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = result.Detail! }),
        _ => Conflict(new MessageResponse { Message = result.Detail ?? "That move is not open." }),
    };
}

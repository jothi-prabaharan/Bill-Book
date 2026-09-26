using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Api.Services;
using Shared.Kernel.Approvals;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Purchase.Api.Controllers;

/// <summary>
/// Approval chains for purchase orders, bills and debit notes (TK-100).
///
/// <b>Submitting is an edit</b> (<c>purchase.edit</c>): the requester sends the
/// document up. <b>Acting and reading need only <c>purchase.view</c></b>, because
/// being the level's approver is the authority (design, decision 7); the chain
/// refuses anyone else. Posting still needs <c>purchase.approve</c>, on its own route.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/purchase")]
[RequireApp(App.RetailErp)]
public sealed class PurchaseApprovalsController : ControllerBase
{
    private const string Documents = "{document:regex(^(purchase-orders|bills|debit-notes)$)}";

    private readonly PurchaseApprovalService _approvals;

    public PurchaseApprovalsController(PurchaseApprovalService approvals) => _approvals = approvals;

    [HttpPost(Documents + "/{id:long}/submit")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Submit(string document, long id, CancellationToken ct) =>
        Respond(await _approvals.SubmitAsync(PurchaseApprovalService.KindOf(document)!.Value, id, ct));

    [HttpGet(Documents + "/{id:long}/approval")]
    [PermissionAction("view")]
    public async Task<IActionResult> Chain(string document, long id, CancellationToken ct) =>
        await _approvals.ChainAsync(PurchaseApprovalService.KindOf(document)!.Value, id, ct) is { } chain
            ? Ok(chain)
            : NotFound();

    [HttpPost(Documents + "/{id:long}/approval")]
    [PermissionAction("view")]
    public async Task<IActionResult> Act(string document, long id, [FromBody] ApprovalActionRequest request, CancellationToken ct) =>
        Respond(await _approvals.ActAsync(
            PurchaseApprovalService.KindOf(document)!.Value, id, request.Action, request.Comments, ct));

    /// <summary>
    /// Purchase documents waiting on the signed-in user. Each service answers
    /// for its own under its own prefix, and the inbox (TK-103) asks them all.
    /// </summary>
    [HttpGet("approvals/mine")]
    [PermissionAction("view")]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await _approvals.MineAsync(ct));

    private IActionResult Respond(PurchaseApprovalResult result) => result.Outcome switch
    {
        PurchaseApprovalOutcome.Ok => Ok(new { approvalStatus = result.ApprovalStatus }),
        PurchaseApprovalOutcome.NotFound => NotFound(),
        PurchaseApprovalOutcome.NoWorkflow => Ok(new { approvalStatus = (string?)null, message = result.Detail }),
        PurchaseApprovalOutcome.NotTheApprover => StatusCode(StatusCodes.Status403Forbidden, new MessageResponse { Message = result.Detail! }),
        PurchaseApprovalOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = result.Detail! }),
        _ => Conflict(new MessageResponse { Message = result.Detail ?? "That move is not open." }),
    };
}

public sealed class ApprovalActionRequest
{
    [EnumDataType(typeof(ApprovalAction), ErrorMessage = "Choose approve, reject or send back.")]
    public ApprovalAction Action { get; set; } = ApprovalAction.Approve;

    [MaxLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    public string? Comments { get; set; }
}

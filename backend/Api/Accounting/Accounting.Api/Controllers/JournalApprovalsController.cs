using Accounting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Approvals;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// Approval chains for manual journals (TK-101).
///
/// <b>Submitting is an edit</b> (<c>accounting.edit</c>). <b>Acting and reading need
/// only <c>accounting.view</c></b>, because being the level's approver is the
/// authority; the chain refuses anyone else. Posting stays on its own route,
/// which refuses while a workflow applies and has not approved.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/journals")]
[RequireApp(App.RetailErp)]
public sealed class JournalApprovalsController : ControllerBase
{
    private const ApprovalRequestKind Kind = ApprovalRequestKind.ManualJournal;

    private readonly AccountingApprovalService _approvals;

    public JournalApprovalsController(AccountingApprovalService approvals) => _approvals = approvals;

    [HttpPost("{id:long}/submit")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct) =>
        Respond(await _approvals.SubmitAsync(Kind, id, ct));

    [HttpGet("{id:long}/approval")]
    [PermissionAction("view")]
    public async Task<IActionResult> Chain(long id, CancellationToken ct) =>
        await _approvals.ChainAsync(Kind, id, ct) is { } chain ? Ok(chain) : NotFound();

    [HttpPost("{id:long}/approval")]
    [PermissionAction("view")]
    public async Task<IActionResult> Act(long id, [FromBody] DocumentApprovalActionRequest request, CancellationToken ct) =>
        Respond(await _approvals.ActAsync(Kind, id, request.Action, request.Comments, ct));

    /// <summary>Manual journals waiting on the signed-in user, for the approvals inbox (TK-103).</summary>
    [HttpGet("approvals/mine")]
    [PermissionAction("view")]
    public async Task<IActionResult> Mine(CancellationToken ct) =>
        Ok((await _approvals.MineAsync(ct)).Where(i => i.RequestKind == Kind.ToString()));

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

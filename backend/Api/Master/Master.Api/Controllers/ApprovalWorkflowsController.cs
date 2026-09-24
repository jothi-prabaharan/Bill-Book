using Master.Api.Services;
using Master.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// Settings › Approval workflows (D-26, TK-99): every app's chains, configured
/// in one place. Reading takes <c>settings.view</c> and saving <c>settings.edit</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("settings")]
[RequireApp(App.All)]
[Route("api/approval-workflows")]
public sealed class ApprovalWorkflowsController : ControllerBase
{
    private readonly ApprovalWorkflowService _workflows;

    public ApprovalWorkflowsController(ApprovalWorkflowService workflows) => _workflows = workflows;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _workflows.ListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveApprovalWorkflowRequest request, CancellationToken ct) =>
        Answer(await _workflows.SaveAsync(null, request, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveApprovalWorkflowRequest request, CancellationToken ct) =>
        Answer(await _workflows.SaveAsync(id, request, ct));

    private IActionResult Answer(ApprovalWorkflowResult result) => result.Outcome switch
    {
        ApprovalWorkflowOutcome.Ok => Ok(new { id = result.Id }),
        ApprovalWorkflowOutcome.NotFound => NotFound(),
        _ => UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "Complete every level." }),
    };
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services;
using Platform.Entity.Models;
using Shared.Kernel.Ordering;

namespace Platform.Api.Controllers;

/// <summary>
/// Branches under an organization.
///
/// The org id is in the route because that is how every organization-scoped
/// endpoint in this service is shaped — but it is checked against the caller's
/// <c>org_id</c> claim on every action, and a mismatch is <see cref="Forbid"/>
/// rather than <see cref="NotFound"/>. <c>plt</c> lives in the master database
/// alongside every other customer's rows, so the claim check is the only thing
/// separating them; there is no row-level security to fall back on.
/// </summary>
[ApiController]
[Authorize]
[Route("api/organizations/{orgId:guid}/branches")]
public sealed class BranchesController : ControllerBase
{
    private readonly BranchService _branches;

    public BranchesController(BranchService branches) => _branches = branches;

    [HttpGet]
    public async Task<IActionResult> List(
        Guid orgId, [FromQuery] bool includeInactive, CancellationToken ct)
    {
        if (!CallerOwns(orgId))
        {
            return Forbid();
        }

        return Ok(await _branches.ListAsync(orgId, includeInactive, ct));
    }

    [HttpGet("{branchId:long}")]
    public async Task<IActionResult> Get(Guid orgId, long branchId, CancellationToken ct)
    {
        if (!CallerOwns(orgId))
        {
            return Forbid();
        }

        BranchListItem? branch = await _branches.GetAsync(orgId, branchId, ct);
        return branch is null ? NotFound() : Ok(branch);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid orgId, [FromBody] SaveBranchRequest request, CancellationToken ct)
    {
        if (!CallerOwns(orgId))
        {
            return Forbid();
        }

        SaveBranchResult result = await _branches.CreateAsync(orgId, request, ct);
        return result.Outcome == SaveBranchOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { orgId, branchId = result.BranchId }, result)
            : Respond(result.Outcome);
    }

    [HttpPut("{branchId:long}")]
    public async Task<IActionResult> Update(
        Guid orgId, long branchId, [FromBody] SaveBranchRequest request, CancellationToken ct)
    {
        if (!CallerOwns(orgId))
        {
            return Forbid();
        }

        SaveBranchResult result = await _branches.UpdateAsync(orgId, branchId, request, ct);
        return result.Outcome == SaveBranchOutcome.Ok ? NoContent() : Respond(result.Outcome);
    }

    [HttpDelete("{branchId:long}")]
    public async Task<IActionResult> Deactivate(Guid orgId, long branchId, CancellationToken ct)
    {
        if (!CallerOwns(orgId))
        {
            return Forbid();
        }

        SaveBranchOutcome outcome = await _branches.DeactivateAsync(orgId, branchId, ct);
        return outcome == SaveBranchOutcome.Ok ? NoContent() : Respond(outcome);
    }

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(
        Guid orgId, [FromBody] ReorderRequest request, CancellationToken ct)
    {
        if (!CallerOwns(orgId))
        {
            return Forbid();
        }

        SaveBranchOutcome outcome = await _branches.ReorderAsync(orgId, request, ct);
        return outcome == SaveBranchOutcome.Ok ? NoContent() : Respond(outcome);
    }

    /// <summary>
    /// True when the caller's token names the organization in the route. A token
    /// without an org_id claim owns nothing — it is the pre-auth token, issued
    /// before an organization has been chosen.
    /// </summary>
    private bool CallerOwns(Guid orgId) =>
        Guid.TryParse(User.FindFirstValue("org_id"), out Guid claimed) && claimed == orgId;

    private IActionResult Respond(SaveBranchOutcome outcome) =>
        outcome switch
        {
            SaveBranchOutcome.NotFound => NotFound(),
            SaveBranchOutcome.OrganizationNotFound => NotFound(),
            SaveBranchOutcome.Forbidden => Forbid(),
            SaveBranchOutcome.DuplicateCode => Conflict(new MessageResponse
            {
                Message = "Another branch already uses that code.",
            }),
            SaveBranchOutcome.DuplicateName => Conflict(new MessageResponse
            {
                Message = "Another branch already uses that name.",
            }),
            SaveBranchOutcome.GstinStateMismatch => BadRequest(new MessageResponse
            {
                Message = "The GSTIN's first two digits must match the branch's state.",
            }),
            SaveBranchOutcome.HeadOfficeRequired => BadRequest(new MessageResponse
            {
                Message = "Every organization needs a head office. Make another branch the "
                    + "head office first.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}

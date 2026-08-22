using Master.Api.Services;
using Master.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("settings")]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly UserService _users;
    private readonly ICurrentUser _currentUser;

    public UsersController(UserService users, ICurrentUser currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (_currentUser.OrgId is not Guid orgId)
        {
            return Forbid();
        }

        return Ok(await _users.ListAsync(orgId, ct));
    }

    /// <summary>Creates the user and emails an invitation link. Never a temporary password.</summary>
    [HttpPost]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest request, CancellationToken ct)
    {
        if (_currentUser.OrgId is not Guid orgId)
        {
            return Forbid();
        }

        InviteUserResult result = await _users.InviteAsync(orgId, _currentUser.CustomerId, request, ct);
        return result.Outcome switch
        {
            InviteOutcome.Ok => Ok(new { userId = result.UserId }),
            InviteOutcome.OrgNotFound => NotFound(),
            InviteOutcome.AlreadyMember => Conflict(new MessageResponse
            {
                Message = "That person already has access to this organization.",
            }),
            InviteOutcome.UserLimitReached => Conflict(new MessageResponse
            {
                Message = "Your plan's user limit has been reached. Upgrade to add more users.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{userId:guid}/resend-invite")]
    public async Task<IActionResult> ResendInvite(Guid userId, CancellationToken ct)
    {
        if (_currentUser.OrgId is not Guid orgId)
        {
            return Forbid();
        }

        bool sent = await _users.ResendInviteAsync(orgId, _currentUser.CustomerId, userId, ct);
        return sent ? NoContent() : NotFound();
    }

    /// <summary>Revokes org access by deactivating the assignment — the user row survives.</summary>
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Revoke(Guid userId, CancellationToken ct)
    {
        if (_currentUser.OrgId is not Guid orgId || _currentUser.UserId is not Guid actingUserId)
        {
            return Forbid();
        }

        RevokeOutcome outcome = await _users.RevokeAsync(orgId, actingUserId, userId, ct);
        return outcome switch
        {
            RevokeOutcome.Ok => NoContent(),
            RevokeOutcome.NotFound => NotFound(),
            RevokeOutcome.CannotRevokeSelf => BadRequest(new MessageResponse
            {
                Message = "You cannot revoke your own access.",
            }),
            RevokeOutcome.LastOwner => BadRequest(new MessageResponse
            {
                Message = "This is the last active Owner. Assign another Owner first.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}

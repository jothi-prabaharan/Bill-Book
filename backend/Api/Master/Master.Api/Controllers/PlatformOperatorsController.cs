using Master.Api.Services;
using Master.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// Platform operators, managed by operators (D-01, TK-13). The only route that
/// can set <c>IsPlatformOperator</c> after bootstrap, and it needs
/// <c>platform.edit</c> — which no role grants and only an operator's token
/// carries — so no tenant user, Owner included, can reach it.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/platform-operators")]
public sealed class PlatformOperatorsController : ControllerBase
{
    private readonly PlatformOperatorService _operators;
    private readonly ICurrentUser _user;

    public PlatformOperatorsController(PlatformOperatorService operators, ICurrentUser user)
    {
        _operators = operators;
        _user = user;
    }

    [RequirePermission("platform.view")]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _operators.ListAsync(ct));

    /// <summary>Grants or revokes. Takes effect at the user's next sign-in or refresh.</summary>
    [RequirePermission("platform.edit")]
    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Set(
        Guid userId, [FromBody] SetPlatformOperatorRequest request, CancellationToken ct) =>
        await _operators.SetAsync(userId, request.IsPlatformOperator, _user.UserId, ct) switch
        {
            PlatformOperatorOutcome.Ok => NoContent(),
            PlatformOperatorOutcome.NotFound => NotFound(),
            PlatformOperatorOutcome.CannotRevokeSelf => BadRequest(new MessageResponse
            {
                Message = "You cannot remove your own operator access. Ask another operator.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}

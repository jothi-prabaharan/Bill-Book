using Fee.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Fee.Api.Controllers;

/// <summary>
/// A guardian's fee demands and receipts in the parent portal (S9, TK-69).
/// The contact comes from the School portal token, never from the route.
/// </summary>
[ApiController]
[Authorize]
[RequirePortalAccess]
[RequireApp(App.School)]
[Route("api/portal/school/fees")]
public sealed class PortalFeesController : ControllerBase
{
    private readonly PortalFeeService _portal;

    public PortalFeesController(PortalFeeService portal) => _portal = portal;

    private long ContactId => long.Parse(User.FindFirst(RequirePortalAccessAttribute.ContactClaim)!.Value);

    [HttpGet("demands")]
    public async Task<IActionResult> Demands(CancellationToken ct) => Ok(await _portal.DemandsAsync(ContactId, ct));

    [HttpGet("receipts")]
    public async Task<IActionResult> Receipts(CancellationToken ct) => Ok(await _portal.ReceiptsAsync(ContactId, ct));
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Services;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The client portal's dashboard (TK-95): what the contact owes and how much of
/// it is overdue, and what they have traded this financial year and in all.
/// </summary>
[ApiController]
[Authorize]
[RequirePortalAccess]
[Route("api/portal/summary")]
[RequireApp(App.RetailErp)]
public sealed class PortalSummaryController : ControllerBase
{
    private readonly PortalAccountService _accounts;

    public PortalSummaryController(PortalAccountService accounts) => _accounts = accounts;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        long contactId = long.Parse(User.FindFirst(RequirePortalAccessAttribute.ContactClaim)!.Value);
        return Ok(await _accounts.SummaryAsync(contactId, ct));
    }
}

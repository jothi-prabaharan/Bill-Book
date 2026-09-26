using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Services;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// A contact's statement in the client portal: the receivable side, and the
/// payable side when they are also a vendor (TK-95). The contact is the portal
/// token's and nobody else's.
/// </summary>
[ApiController]
[Authorize]
[RequirePortalAccess]
[Route("api/portal/statements")]
[RequireApp(App.RetailErp)]
public sealed class PortalStatementsController : ControllerBase
{
    private readonly PortalAccountService _accounts;

    public PortalStatementsController(PortalAccountService accounts) => _accounts = accounts;

    [HttpGet]
    public async Task<IActionResult> GetContactStatement(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        // [RequirePortalAccess] has already refused anything without a usable
        // contact_id, so this reads the value rather than re-checking for it.
        long contactId = long.Parse(User.FindFirst(RequirePortalAccessAttribute.ContactClaim)!.Value);
        return Ok(await _accounts.StatementAsync(contactId, fromDate, toDate, ct));
    }
}

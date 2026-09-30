using Sales.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// The branch's custom fields on sales documents (TK-112), for the document
/// forms: <c>?kind=Invoice</c>. Under the module's own <c>sales.view</c> so a
/// form needs no <c>settings.view</c>; only this service's kinds are served.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[RequireBusinessType(BusinessType.General, BusinessType.Pharma, BusinessType.Jewellery)]
[Route("api/sales/custom-fields")]
public sealed class SalesCustomFieldsController : ControllerBase
{
    private static readonly HashSet<CustomFieldEntityKind> Kinds =
    [
        CustomFieldEntityKind.Quote,
        CustomFieldEntityKind.SalesOrder,
        CustomFieldEntityKind.DeliveryChallan,
        CustomFieldEntityKind.Invoice,
        CustomFieldEntityKind.CreditNote,
    ];

    [HttpGet]
    public async Task<IActionResult> ForKind([FromQuery] string? kind, [FromServices] ICustomFieldDefinitions definitions, CancellationToken ct)
    {
        if (!Enum.TryParse(kind, ignoreCase: true, out CustomFieldEntityKind parsed) || !Kinds.Contains(parsed))
        {
            return BadRequest(new MessageResponse { Message = "Name one of this service's document kinds." });
        }

        return await definitions.GetAsync(parsed, ct) is { } fields ? Ok(fields) : StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}

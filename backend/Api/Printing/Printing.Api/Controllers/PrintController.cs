using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Printing.Api.Services;
using Printing.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Printing.Api.Controllers;

/// <summary>
/// Renders a document that its own service pushes.
///
/// <b>Signed-in only, with no module permission, and that is deliberate.</b>
/// The authority to print an invoice is the authority to read it, and that was
/// already checked where it lives: Sales refused anyone without
/// <c>sales.view</c> before it could build the payload. Printing is handed the
/// document's data, not asked for it, so a module guard here would check a
/// second time something it cannot see — and whichever module it named would
/// lock out every other document type, since one route prints twelve of them
/// across three modules. What this route reads of its own is the branch's
/// template, which is the letterhead printed on every document the branch hands
/// out; the query filter keeps it to the caller's branch.
///
/// <para>
/// Named as an exemption in <c>EndpointGuardTests</c>, the way the menu and the
/// display formats are in Master, so the absence of a guard is a written
/// decision rather than a missing attribute.
/// </para>
/// </summary>
[ApiController]
[Authorize]
[Route("api/print")]
[RequireApp(App.All)]
public sealed class PrintController : ControllerBase
{
    private readonly PrintTemplateService _templates;

    public PrintController(PrintTemplateService templates) => _templates = templates;

    [HttpPost("render")]
    public async Task<IActionResult> Render([FromBody] RenderPrintRequest request, CancellationToken ct)
    {
        PrintTemplateResult<RenderPrintResponse> result = await _templates.RenderAsync(request, ct);

        return result.Outcome switch
        {
            PrintTemplateOutcome.Ok => Ok(result.Value),

            PrintTemplateOutcome.InvalidDocumentType => UnprocessableEntity(new PrintTemplateError
            {
                Code = "INVALID_DOCUMENT_TYPE",
                Message = "That document type has no printable layout.",
            }),

            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}

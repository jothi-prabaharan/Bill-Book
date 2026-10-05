using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Printing.Entity.Models;
using Printing.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Printing.Api.Controllers;

/// <summary>
/// Labels at an exact size (TK-159): the branch's label sizes, and a page of
/// labels the browser prints at that size through the label printer's own
/// driver.
///
/// Under the inventory module, because labels are what stock is marked with:
/// reading the sizes needs <c>inventory.view</c>, changing them
/// <c>inventory.edit</c>, and printing <c>inventory.print</c>. The content is
/// the caller's to send — Printing never reads an item.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("inventory")]
[Route("api/print/labels")]
[RequireBusinessType(AllowStandalone = true)]
public sealed class LabelsController : ControllerBase
{
    private readonly LabelRepository _labels;

    public LabelsController(LabelRepository labels) => _labels = labels;

    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct) => Ok(await _labels.ListAsync(ct));

    [HttpPut("templates/{labelTemplateId:long}")]
    public async Task<IActionResult> SaveTemplate(long labelTemplateId, [FromBody] SaveLabelTemplateRequest request, CancellationToken ct) =>
        await _labels.SaveAsync(labelTemplateId, request, ct) == LabelOutcome.Ok ? NoContent() : NotFound();

    /// <summary>200 with the page; 404 for a size not in this branch; 422 past the per-request limit.</summary>
    [HttpPost]
    [PermissionAction("print")]
    public async Task<IActionResult> Print([FromBody] PrintLabelsRequest request, CancellationToken ct)
    {
        LabelResult<PrintLabelsResponse> result = await _labels.RenderAsync(request, ct);

        return result.Outcome switch
        {
            LabelOutcome.Ok => Ok(result.Value),
            LabelOutcome.NotFound => NotFound(),
            LabelOutcome.TooMany => UnprocessableEntity(new
            {
                message = $"At most {LabelRendererRepository.MaxLabels} labels can be printed at once. Print them in parts.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}

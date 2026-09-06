using Master.Api.Services;
using Master.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Printing;

namespace Master.Api.Controllers;

/// <summary>
/// The print template master, and the placeholder catalogue the editor's
/// Insert-field panel reads.
///
/// <b>Guarded as <c>settings</c>.</b> The specification names a
/// <c>print_template.manage</c> permission, which cannot be declared here: both
/// the module list and the action list are closed sets seeded in AdminDbContext,
/// and a controller naming an unseeded module is a locked door for every role
/// including Owner — that shipped once already. <c>RequireModulePermission</c>
/// derives the action from the HTTP method, so reading takes
/// <c>settings.view</c> and every mutation <c>settings.edit</c>, which is the
/// split the specification asks for under the names this product has.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("settings")]
[Route("api/print-templates")]
public sealed class PrintTemplatesController : ControllerBase
{
    private readonly PrintTemplateService _templates;

    public PrintTemplatesController(PrintTemplateService templates) => _templates = templates;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? docType, CancellationToken ct) =>
        Ok(await _templates.ListAsync(docType, ct));

    /// <summary>The printable document types, for the editor's picker.</summary>
    [HttpGet("document-types")]
    public IActionResult DocumentTypes() =>
        Ok(DocumentTypeCatalog.All.Select(p => new { p.Code, p.Name }));

    /// <summary>
    /// The Insert-field panel: placeholders grouped, group order preserved, list
    /// groups flagged so the panel can mark them with the repeat icon.
    /// </summary>
    [HttpGet("{docType}/placeholders")]
    public IActionResult Placeholders(string docType)
    {
        if (!DocumentTypeCatalog.IsPrintable(docType))
        {
            return Refuse(PrintTemplateOutcome.InvalidDocumentType, []);
        }

        return Ok(PlaceholderCatalog.Grouped(docType));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        Respond(await _templates.GetAsync(id, ct), Ok);

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePrintTemplateRequest request, CancellationToken ct) =>
        Respond(
            await _templates.CreateAsync(request, ct),
            detail => StatusCode(StatusCodes.Status201Created, detail));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id, [FromBody] UpdatePrintTemplateRequest request, CancellationToken ct) =>
        Respond(await _templates.UpdateAsync(id, request, ct), Ok);

    [HttpPatch("{id:long}/default")]
    public async Task<IActionResult> SetDefault(long id, CancellationToken ct) =>
        Respond(await _templates.SetDefaultAsync(id, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        Respond(await _templates.DeleteAsync(id, ct));

    [HttpPost("{id:long}/reset")]
    public async Task<IActionResult> Reset(long id, CancellationToken ct) =>
        Respond(await _templates.ResetAsync(id, ct), Ok);

    [HttpPost("{id:long}/preview")]
    public async Task<IActionResult> Preview(
        long id, [FromBody] PreviewPrintTemplateRequest? request, CancellationToken ct) =>
        Respond(await _templates.PreviewAsync(id, request ?? new PreviewPrintTemplateRequest(), ct), Ok);

    private IActionResult Respond<T>(PrintTemplateResult<T> result, Func<T, IActionResult> onOk) =>
        result.Outcome == PrintTemplateOutcome.Ok
            ? onOk(result.Value!)
            : Refuse(result.Outcome, result.Details);

    private IActionResult Respond(PrintTemplateOutcome outcome) =>
        outcome == PrintTemplateOutcome.Ok ? NoContent() : Refuse(outcome, []);

    /// <summary>
    /// The specification's codes, mapped once. A refusal carries both the code
    /// and a sentence, because the code is for the client's logic and the
    /// sentence is what a person ends up reading.
    /// </summary>
    private IActionResult Refuse(PrintTemplateOutcome outcome, IReadOnlyList<string> details) => outcome switch
    {
        PrintTemplateOutcome.NotFound => NotFound(),

        PrintTemplateOutcome.NameTaken => Conflict(new PrintTemplateError
        {
            Code = "TEMPLATE_NAME_TAKEN",
            Message = "Another template for this document type already uses that name.",
        }),

        PrintTemplateOutcome.Stale => Conflict(new PrintTemplateError
        {
            Code = "TEMPLATE_STALE",
            Message = "Somebody else saved this template while it was open. "
                + "Reload it and apply the changes again — nothing has been overwritten.",
        }),

        PrintTemplateOutcome.LastTemplate => Conflict(new PrintTemplateError
        {
            Code = "LAST_TEMPLATE",
            Message = "This is the default template for its document type, or the only one left. "
                + "Make another template the default first.",
        }),

        PrintTemplateOutcome.InvalidSegmentHtml => UnprocessableEntity(new PrintTemplateError
        {
            Code = "INVALID_SEGMENT_HTML",
            Message = details.Count > 0
                ? $"The layout contains markup that cannot be printed: {string.Join("; ", details)}."
                : "The layout contains markup that cannot be printed.",
        }),

        PrintTemplateOutcome.InvalidGeometry => UnprocessableEntity(new PrintTemplateError
        {
            Code = "INVALID_GEOMETRY",
            Message = details.Count > 0
                ? string.Join(" ", details)
                : "The page settings leave nothing to print in.",
        }),

        PrintTemplateOutcome.InvalidDocumentType => UnprocessableEntity(new PrintTemplateError
        {
            Code = "INVALID_DOCUMENT_TYPE",
            Message = "That document type has no printable layout.",
        }),

        PrintTemplateOutcome.PreviewNotAvailableHere => UnprocessableEntity(new PrintTemplateError
        {
            Code = "PREVIEW_NEEDS_DOCUMENT_SERVICE",
            Message = "Previewing against a real document is served by that document's own print "
                + "endpoint. Omit sampleDocId to preview against sample data.",
        }),

        _ => StatusCode(StatusCodes.Status500InternalServerError),
    };
}

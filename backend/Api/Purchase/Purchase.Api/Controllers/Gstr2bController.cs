using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Api.Services.Gstr2b;
using Purchase.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Purchase.Api.Controllers;

/// <summary>
/// GSTR-2B reconciliation (TK-116): import the month's 2B JSON from the GST
/// portal, and read what matching it against the purchase register found.
/// Reading needs <c>purchase.view</c>; importing and deleting need <c>purchase.edit</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/purchase/gstr2b")]
[RequireApp(App.RetailErp)]
public sealed class Gstr2bController : ControllerBase
{
    /// <summary>A branch's month of supplier invoices; a larger file is not a 2B.</summary>
    private const long MaxUploadBytes = 20 * 1024 * 1024;

    private readonly Gstr2bService _gstr2b;

    public Gstr2bController(Gstr2bService gstr2b) => _gstr2b = gstr2b;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _gstr2b.ListAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _gstr2b.GetAsync(id, ct) is Gstr2bImportDetail detail ? Ok(detail) : NotFound();

    [HttpPost]
    [PermissionAction("edit")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new MessageResponse { Message = "Choose the GSTR-2B JSON file to import." });
        }

        if (file.Length > MaxUploadBytes)
        {
            return BadRequest(new MessageResponse { Message = "That file is too large to be a month's GSTR-2B." });
        }

        string json;
        using (StreamReader reader = new(file.OpenReadStream()))
        {
            json = await reader.ReadToEndAsync(ct);
        }

        Gstr2bImportResult result = await _gstr2b.ImportAsync(Path.GetFileName(file.FileName), json, ct);

        return result.Outcome == Gstr2bImportOutcome.Ok
            ? Ok(await _gstr2b.GetAsync(result.Gstr2bImportId, ct))
            : UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "The file could not be imported." });
    }

    [HttpDelete("{id:long}")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        await _gstr2b.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

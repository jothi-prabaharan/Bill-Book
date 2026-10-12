using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Printing.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Printing.Api.Controllers;

/// <summary>
/// The print templates the create pages for purchase orders, goods receipts, bills
/// and debit notes offer (TK-192), behind <c>purchase.view</c> rather than the
/// template editor's <c>settings.view</c>: choosing a layout for a document is the
/// document's business, editing one is the branch's.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/print-templates/choices/purchase")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.School, BusinessType.Service)]
public sealed class PurchasePrintChoicesController : ControllerBase
{
    private static readonly string[] DocumentTypes = ["POR", "GRN", "BIL", "DBN"];
    private readonly PrintTemplateRepository _templates;

    public PurchasePrintChoicesController(PrintTemplateRepository templates) => _templates = templates;

    /// <summary>
    /// One of this module's own document types; any other is not found here.
    /// </summary>
    [HttpGet("{docType}")]
    public async Task<IActionResult> Templates(string docType, CancellationToken ct)
    {
        string code = docType.Trim().ToUpperInvariant();
        return DocumentTypes.Contains(code) ? Ok(await _templates.ChoicesAsync(code, ct)) : NotFound();
    }
}

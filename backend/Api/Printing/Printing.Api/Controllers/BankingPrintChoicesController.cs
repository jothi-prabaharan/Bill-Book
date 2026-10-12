using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Printing.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Printing.Api.Controllers;

/// <summary>
/// The print templates the create pages for receipts and payments offer (TK-192),
/// behind <c>banking.view</c> rather than the template editor's
/// <c>settings.view</c>: choosing a layout for a document is the document's
/// business, editing one is the branch's.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("banking")]
[Route("api/print-templates/choices/banking")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.School, BusinessType.Service)]
public sealed class BankingPrintChoicesController : ControllerBase
{
    private static readonly string[] DocumentTypes = ["RCM", "SPM"];
    private readonly PrintTemplateRepository _templates;

    public BankingPrintChoicesController(PrintTemplateRepository templates) => _templates = templates;

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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Fixed Asset Reconciliation Report (TK-255): dedicated endpoint and page at
/// <c>/reports/fixed-asset-reconciliation</c>.
/// Whether the asset register and the ledger agree, account by account:
/// cost and accumulated depreciation per category against the GL.
/// Guarded with AddOn.FixedAssets alone (no RequireBusinessType beside it).
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/fixed-asset-reconciliation")]
[RequireAddOn(AddOn.FixedAssets)]
[RequireModulePermission("reports")]
public sealed class FixedAssetReconciliationReportController : ControllerBase
{
    private readonly FixedAssetReconciliationReportRepository _report;

    public FixedAssetReconciliationReportController(FixedAssetReconciliationReportRepository report) => _report = report;

    /// <summary>Runs the fixed asset reconciliation report for a required period.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] FixedAssetReconciliationReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach (var (field, message) in FixedAssetReconciliationReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }
}

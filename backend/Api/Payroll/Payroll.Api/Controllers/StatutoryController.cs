using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payroll.Api.Services;
using Payroll.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Payroll.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("payroll")]
[RequireApp(App.Payroll)]
[Route("api/payroll/statutory")]
public sealed class StatutoryController : ControllerBase
{
    private readonly StatutoryService _statutory;

    public StatutoryController(StatutoryService statutory) => _statutory = statutory;

    [HttpGet("pf")]
    public async Task<IActionResult> GetPf(CancellationToken ct) =>
        Ok(await _statutory.GetPfSettingAsync(ct));

    [HttpPost("pf")]
    public async Task<IActionResult> SavePf([FromBody] SavePfSettingRequest request, CancellationToken ct) =>
        Ok(new { id = await _statutory.SavePfSettingAsync(request, ct) });

    [HttpGet("esi")]
    public async Task<IActionResult> GetEsi(CancellationToken ct) =>
        Ok(await _statutory.GetEsiSettingAsync(ct));

    [HttpPost("esi")]
    public async Task<IActionResult> SaveEsi([FromBody] SaveEsiSettingRequest request, CancellationToken ct) =>
        Ok(new { id = await _statutory.SaveEsiSettingAsync(request, ct) });

    [HttpGet("pt-slabs")]
    public async Task<IActionResult> ListPtSlabs([FromQuery] int? stateId, CancellationToken ct) =>
        Ok(await _statutory.ListPtSlabsAsync(stateId, ct));

    [HttpPost("pt-slabs")]
    public async Task<IActionResult> SavePtSlab([FromBody] SaveProfessionalTaxSlabRequest request, CancellationToken ct) =>
        Ok(new { id = await _statutory.SavePtSlabAsync(request, ct) });

    [HttpGet("returns/ecr/{runId:long}")]
    public async Task<IActionResult> DownloadPfEcr(long runId, CancellationToken ct)
    {
        string text = await _statutory.GeneratePfEcrAsync(runId, ct);
        return File(System.Text.Encoding.UTF8.GetBytes(text), "text/plain", $"PF_ECR_Run_{runId}.txt");
    }

    [HttpGet("returns/esi/{runId:long}")]
    public async Task<IActionResult> DownloadEsiReturn(long runId, CancellationToken ct)
    {
        string text = await _statutory.GenerateEsiReturnAsync(runId, ct);
        return File(System.Text.Encoding.UTF8.GetBytes(text), "text/csv", $"ESI_Return_Run_{runId}.csv");
    }

    [HttpGet("returns/pt/{runId:long}")]
    public async Task<IActionResult> DownloadPtChallan(long runId, [FromQuery] int stateId, CancellationToken ct)
    {
        string text = await _statutory.GeneratePtChallanAsync(runId, stateId, ct);
        return Content(text, "text/plain", System.Text.Encoding.UTF8);
    }
}

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
[Route("api/payroll/runs")]
public sealed class PayrollRunsController : ControllerBase
{
    private readonly PayrollRunService _runs;

    public PayrollRunsController(PayrollRunService runs) => _runs = runs;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _runs.ListRunsAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        PayrollRunView? run = await _runs.GetRunAsync(id, ct);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("{id:long}/payslips")]
    public async Task<IActionResult> GetPayslips(long id, CancellationToken ct) =>
        Ok(await _runs.GetPayslipsAsync(id, ct));

    [HttpPost("process")]
    public async Task<IActionResult> Process([FromBody] ProcessPayrollRunRequest request, CancellationToken ct)
    {
        long id = await _runs.ProcessRunAsync(request.Month, ct);
        return Ok(new { id });
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken ct)
    {
        await _runs.ApproveRunAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken ct)
    {
        await _runs.PostRunAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/mark-paid")]
    public async Task<IActionResult> MarkPaid(long id, CancellationToken ct)
    {
        await _runs.MarkPaidAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/reverse")]
    public async Task<IActionResult> Reverse(long id, CancellationToken ct)
    {
        await _runs.ReverseRunAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/export/bank")]
    public async Task<IActionResult> ExportBankFile(long id, CancellationToken ct)
    {
        string csv = await _runs.ExportBankCsvAsync(id, ct);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"BankFile_Run_{id}.csv");
    }

    [HttpGet("{id:long}/export/tally")]
    public async Task<IActionResult> ExportTallyXml(long id, CancellationToken ct)
    {
        string xml = await _runs.ExportTallyXmlAsync(id, ct);
        return Content(xml, "application/xml", System.Text.Encoding.UTF8);
    }
}

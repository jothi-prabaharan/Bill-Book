using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/outstanding")]
public class OutstandingController : ControllerBase
{
    private readonly OutstandingService _outstanding;

    public OutstandingController(OutstandingService outstanding)
    {
        _outstanding = outstanding;
    }

    [HttpGet("aging")]
    public async Task<IActionResult> AgingSummary(CancellationToken ct)
    {
        var summary = await _outstanding.GetAgingSummaryAsync(ct);
        return Ok(summary);
    }

    [HttpGet("invoices/{customerId:long}")]
    public async Task<IActionResult> CustomerInvoices(long customerId, CancellationToken ct)
    {
        var invoices = await _outstanding.GetUnpaidInvoicesAsync(customerId, ct);
        return Ok(invoices);
    }
}

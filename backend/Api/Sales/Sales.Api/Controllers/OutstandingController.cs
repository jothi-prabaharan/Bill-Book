using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Repository.Repositories;
using Sales.Repository.Clients;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Sales.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/outstanding")]
[RequireApp(App.RetailErp)]
public class OutstandingController : ControllerBase
{
    private readonly OutstandingRepository _outstanding;

    public OutstandingController(OutstandingRepository outstanding)
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

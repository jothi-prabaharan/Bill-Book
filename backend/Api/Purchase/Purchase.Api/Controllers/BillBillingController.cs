using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Api.Services;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Purchase.Api.Controllers;

/// <summary>The bill lines a project has left to charge on to its client (TK-106), for the invoice's "Add project items".</summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/purchase/bills/billable")]
[RequireApp(App.RetailErp)]
public sealed class BillBillingController : ControllerBase
{
    private readonly BillBillingService _billing;

    public BillBillingController(BillBillingService billing) => _billing = billing;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long projectId, CancellationToken ct) =>
        Ok(await _billing.BillableAsync(projectId, ct));
}

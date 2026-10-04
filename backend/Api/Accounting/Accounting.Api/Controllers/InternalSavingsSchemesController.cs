using Accounting.Entity.Models;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// The worker's daily savings scheme run (TK-141): lapses what is overdue past
/// its grace days and says which customers to remind. The worker sends the
/// emails; this decides who gets one. The tenant comes from the body, set
/// before the repository is resolved.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/savings-schemes")]
public sealed class InternalSavingsSchemesController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalSavingsSchemesController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] RunSavingsSchemesRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse { Message = "A customer and an organization are required." });
        }

        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var schemes = _services.GetRequiredService<SavingsSchemeRepository>();
        return Ok(await schemes.RunAsync(request.AsOf, ct));
    }
}

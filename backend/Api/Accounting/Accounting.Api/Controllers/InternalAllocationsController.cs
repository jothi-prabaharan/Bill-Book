using Accounting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("internal/allocations")]
[Authorize]
public class InternalAllocationsController : ControllerBase
{
    private readonly AllocationService _service;

    public InternalAllocationsController(AllocationService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Allocate([FromBody] AllocateTransactionRequest request, CancellationToken ct)
    {
        await _service.AllocateAsync(request, ct);
        return Ok();
    }
}

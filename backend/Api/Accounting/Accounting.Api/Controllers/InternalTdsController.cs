using Shared.Kernel.Apps;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// TDS for other services (TK-117): Purchase asks what a bill owes before it
/// posts, and Master whether a contact's section exists. The branch travels
/// in the request, as on every internal door.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/tds")]
public sealed class InternalTdsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalTdsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("quote")]
    public async Task<IActionResult> Quote([FromBody] TdsQuoteRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse { Message = "A customer and an organization are required to work out TDS." });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        TdsQuoteResult result = await _services.GetRequiredService<TdsRepository>().QuoteAsync(request, ct);

        return result.Outcome switch
        {
            TdsQuoteOutcome.Ok => Ok(result.Quote),
            TdsQuoteOutcome.Refused => UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "TDS could not be worked out." }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse
            {
                Message = "The contact's TDS details could not be read. Try again.",
            }),
        };
    }

    [HttpGet("sections/exists")]
    public async Task<IActionResult> SectionExists(
        [FromQuery] Guid customerId, [FromQuery] Guid orgId, [FromQuery] string? code, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, customerId, orgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse { Message = "A customer and an organization are required." });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        return Ok(await _services.GetRequiredService<TdsSectionRepository>().ExistsAsync(code ?? string.Empty, ct));
    }
}

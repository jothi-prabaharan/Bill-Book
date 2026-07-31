using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services;
using Platform.Entity.Models;

namespace Platform.Api.Controllers;

/// <summary>
/// Internal service-to-service API — consumed by Identity's PlatformDirectory.
/// Must not be routed through the public gateway.
/// </summary>
[ApiController]
[Route("internal/orgs")]
public sealed class InternalOrgsController : ControllerBase
{
    private readonly OrgContextService _context;

    public InternalOrgsController(OrgContextService context) => _context = context;

    [HttpGet("{orgId:guid}/context")]
    public async Task<IActionResult> GetContext(Guid orgId, CancellationToken ct)
    {
        OrgContextResponse? context = await _context.ResolveAsync(orgId, ct);
        return context is null ? NotFound() : Ok(context);
    }
}

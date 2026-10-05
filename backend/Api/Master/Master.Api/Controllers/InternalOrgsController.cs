using Shared.Kernel.Apps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Models;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// Internal service-to-service API — consumed by Identity's PlatformDirectory.
/// Must not be routed through the public gateway.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/orgs")]
public sealed class InternalOrgsController : ControllerBase
{
    private readonly OrgContextRepository _context;

    public InternalOrgsController(OrgContextRepository context) => _context = context;

    [HttpGet("{orgId:guid}/context")]
    public async Task<IActionResult> GetContext(Guid orgId, CancellationToken ct)
    {
        OrgContextResponse? context = await _context.ResolveAsync(orgId, ct);
        return context is null ? NotFound() : Ok(context);
    }
}

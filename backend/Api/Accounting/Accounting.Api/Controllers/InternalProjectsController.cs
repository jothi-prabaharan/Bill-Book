using Accounting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Projects;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Which projects another service's lines may name (TK-104): Sales and
/// Purchase check a line's project here before they store it (TK-105). The
/// branch comes in the body and the query filter keeps the answer inside it,
/// so another branch's project comes back missing, never named.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/projects")]
public sealed class InternalProjectsController : ControllerBase
{
    private const int MaxIds = 500;

    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalProjectsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("exists")]
    public async Task<IActionResult> Exists([FromBody] ProjectExistsRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse { Message = "A customer and an organization are required to look up projects." });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        List<long> ids = [.. request.Ids.Distinct().Take(MaxIds)];
        if (ids.Count == 0)
        {
            return Ok(Array.Empty<ProjectSummary>());
        }

        return Ok(await _services.GetRequiredService<ProjectService>().FindAsync(ids, ct));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Master.Entity.Enums;
using Master.Repository;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// Internal only; not routed through the public gateway.
///
/// Used to hold the tenant directory lookup (which database a customer's books
/// live in) for services that opened a per-customer connection. That resolution
/// is gone now that every service points at the one shared tenant database, so
/// only the background-worker listing below remains.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/customers")]
public sealed class InternalCustomersController : ControllerBase
{
    private readonly AdminDbContext _db;

    public InternalCustomersController(AdminDbContext db) => _db = db;

    /// <summary>
    /// Every branch that is ready to be worked on. Background workers have no
    /// request to take a tenant from, so they walk this list instead.
    /// </summary>
    [HttpGet("active-organizations")]
    public async Task<IActionResult> ActiveOrganizations(CancellationToken ct)
    {
        var rows = await _db.Organizations
            .Where(o => o.Status == TenantStatus.Active)
            .OrderBy(o => o.CustomerId).ThenBy(o => o.OrgId)
            .Select(o => new { o.CustomerId, o.OrgId })
            .ToListAsync(ct);

        return Ok(rows);
    }
}

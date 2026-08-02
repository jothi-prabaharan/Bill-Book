using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Entity.Enums;
using Platform.Repository;
using Shared.Kernel.Internal;

namespace Platform.Api.Controllers;

/// <summary>
/// The tenant directory, for services that need to open a customer's database.
/// Returns the Key Vault reference, never the connection string — each service
/// resolves the credential itself, so it never travels over HTTP.
///
/// Internal only; not routed through the public gateway.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/customers")]
public sealed class InternalCustomersController : ControllerBase
{
    private readonly PlatformDbContext _db;

    public InternalCustomersController(PlatformDbContext db) => _db = db;

    /// <summary>
    /// Every branch that is ready to be worked on, with the database it lives
    /// in. Background workers have no request to take a tenant from, so they
    /// walk this list instead.
    /// </summary>
    [HttpGet("active-organizations")]
    public async Task<IActionResult> ActiveOrganizations(CancellationToken ct)
    {
        var rows = await (
            from o in _db.Organizations
            join d in _db.CustomerDatabases on o.CustomerId equals d.CustomerId
            where o.Status == TenantStatus.Active && d.Status == ProvisioningStatus.Ready
            orderby o.CustomerId, o.OrgId
            select new
            {
                o.CustomerId,
                o.OrgId,
                d.DatabaseName,
            }).ToListAsync(ct);

        return Ok(rows);
    }

    [HttpGet("{customerId:guid}/database")]
    public async Task<IActionResult> GetDatabase(Guid customerId, CancellationToken ct)
    {
        var row = await _db.CustomerDatabases
            .Where(d => d.CustomerId == customerId)
            .Select(d => new
            {
                d.DatabaseName,
                d.ConnectionSecretRef,
                IsReady = d.Status == ProvisioningStatus.Ready,
            })
            .FirstOrDefaultAsync(ct);

        return row is null ? NotFound() : Ok(row);
    }
}

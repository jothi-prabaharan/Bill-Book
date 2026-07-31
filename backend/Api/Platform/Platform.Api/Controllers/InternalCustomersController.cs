using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Entity.Enums;
using Platform.Repository;

namespace Platform.Api.Controllers;

/// <summary>
/// The tenant directory, for services that need to open a customer's database.
/// Returns the Key Vault reference, never the connection string — each service
/// resolves the credential itself, so it never travels over HTTP.
///
/// Internal only; not routed through the public gateway.
/// </summary>
[ApiController]
[Route("internal/customers")]
public sealed class InternalCustomersController : ControllerBase
{
    private readonly PlatformDbContext _db;

    public InternalCustomersController(PlatformDbContext db) => _db = db;

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

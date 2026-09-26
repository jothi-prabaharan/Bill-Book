using System.ComponentModel.DataAnnotations;
using Master.Api.Services;
using Master.Entity.Models;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

namespace Master.Api.Controllers;

/// <summary>
/// Exchanges a portal link's code for a one-hour portal session (TK-94).
///
/// <b>Anonymous, because the code is the credential</b>: a contact has no
/// account to sign in with. It is rate-limited per address, and every failure
/// is the same 401, so the route tells a guesser nothing about which codes
/// exist. The tenant comes from the code (<see cref="PortalAccessService.TryReadCode"/>),
/// and the database is opened only after it is set, which is why the service is
/// resolved in the action and the request carries no filter transaction.
/// </summary>
[ApiController]
[AllowAnonymous]
[RequireApp(App.All)]
[Route("api/portal/session")]
public sealed class PortalSessionController : ControllerBase
{
    private const string Refused = "This portal link has expired or been withdrawn. Ask for a new one.";

    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public PortalSessionController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost]
    [NoTransaction]
    [EnableRateLimiting(PortalRateLimit.Policy)]
    public async Task<IActionResult> Exchange([FromBody] PortalSessionRequest request, CancellationToken ct)
    {
        if (!PortalAccessService.TryReadCode(request.Code, out Guid customerId, out Guid orgId))
        {
            return Unauthorized(new MessageResponse { Message = Refused });
        }

        _tenant.CustomerId = customerId;
        _tenant.OrgId = orgId;

        // The customer's code goes in the token, as on a staff token: stored
        // files are foldered by it, and the portal reads the invoice's PDF (TK-95).
        string? customerCode = await _services.GetRequiredService<AdminDbContext>().Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => c.CustomerCode)
            .FirstOrDefaultAsync(ct);

        _tenant.CustomerCode = customerCode;

        PortalSessionToken? session = await _services
            .GetRequiredService<PortalAccessService>()
            .ExchangeAsync(request.Code!, ct, customerCode);

        if (session is null)
        {
            return Unauthorized(new MessageResponse { Message = Refused });
        }

        return Ok(new PortalSessionResponse
        {
            Token = session.Token,
            ExpiresAt = session.ExpiresAt,
            App = session.App.ToString(),
        });
    }
}

public sealed class PortalSessionRequest
{
    [Required(ErrorMessage = "The portal link's code is required.")]
    [MaxLength(200, ErrorMessage = "The portal link's code cannot exceed 200 characters.")]
    public string? Code { get; set; }
}

public sealed class PortalSessionResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Which portal the session opens: RetailErp or School.</summary>
    public string App { get; set; } = string.Empty;
}

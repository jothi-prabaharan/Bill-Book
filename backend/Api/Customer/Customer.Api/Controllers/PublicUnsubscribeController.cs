using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Repository.Repositories;
using Customer.Repository.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Tenancy;

namespace Customer.Api.Controllers;

/// <summary>
/// Public endpoint for one-click email unsubscribe.
/// Exempted from user token authentication because it is accessed anonymously by email recipients
/// clicking unsubscribe in campaign emails; authenticity is verified by an HMAC-SHA256 signed token.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/unsubscribe")]
public sealed class PublicUnsubscribeController : ControllerBase
{
    private readonly IUnsubscribeTokenSigner _signer;
    private readonly EmailConsentRepository _consentRepo;
    private readonly TenantContext _tenantContext;

    public PublicUnsubscribeController(
        IUnsubscribeTokenSigner signer,
        EmailConsentRepository consentRepo,
        TenantContext tenantContext)
    {
        _signer = signer;
        _consentRepo = consentRepo;
        _tenantContext = tenantContext;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> UnsubscribeGet(string token, CancellationToken ct)
    {
        if (!_signer.TryValidateToken(token, out UnsubscribeTokenPayload? payload) || payload is null)
        {
            return BadRequest(new { message = "Invalid or tampered unsubscribe token." });
        }

        _tenantContext.CustomerId = payload.CustomerId;
        _tenantContext.OrgId = payload.OrgId;

        await _consentRepo.RecordConsentAsync(payload.Email, ConsentStatus.Unsubscribed, ConsentSource.UnsubscribeLink, ct);

        return Ok(new
        {
            message = $"You have been successfully unsubscribed from marketing emails for {payload.Email}.",
            email = payload.Email
        });
    }

    /// <summary>
    /// RFC 8058 One-Click POST handler for List-Unsubscribe=One-Click.
    /// </summary>
    [HttpPost("{token}")]
    public async Task<IActionResult> UnsubscribePost(string token, CancellationToken ct)
    {
        if (!_signer.TryValidateToken(token, out UnsubscribeTokenPayload? payload) || payload is null)
        {
            return BadRequest(new { message = "Invalid or tampered unsubscribe token." });
        }

        _tenantContext.CustomerId = payload.CustomerId;
        _tenantContext.OrgId = payload.OrgId;

        await _consentRepo.RecordConsentAsync(payload.Email, ConsentStatus.Unsubscribed, ConsentSource.UnsubscribeLink, ct);

        return Ok(new
        {
            message = $"You have been successfully unsubscribed from marketing emails for {payload.Email}.",
            email = payload.Email
        });
    }
}

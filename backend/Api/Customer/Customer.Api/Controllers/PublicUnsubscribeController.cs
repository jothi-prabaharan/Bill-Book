using Shared.Kernel.Internal;
using Shared.Kernel.Apps;
using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Repository;
using Customer.Repository.Repositories;
using Customer.Repository.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly CustomerDbContext _db;
    private readonly TenantContext _tenantContext;

    public PublicUnsubscribeController(
        IUnsubscribeTokenSigner signer,
        EmailConsentRepository consentRepo,
        CustomerDbContext db,
        TenantContext tenantContext)
    {
        _signer = signer;
        _consentRepo = consentRepo;
        _db = db;
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

        if (payload.CampaignId.HasValue)
        {
            var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.CampaignId == payload.CampaignId.Value, ct);
            if (campaign is not null)
            {
                campaign.TotalUnsubscribed++;
            }

            if (payload.RecipientId.HasValue)
            {
                var recipient = await _db.CampaignRecipients.FirstOrDefaultAsync(r => r.CampaignRecipientId == payload.RecipientId.Value, ct);
                if (recipient is not null)
                {
                    recipient.UnsubscribedAt = DateTimeOffset.UtcNow;
                }
            }

            await _db.SaveChangesAsync(ct);
        }

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
        return await UnsubscribeGet(token, ct);
    }
}

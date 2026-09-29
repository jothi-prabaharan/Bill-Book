using Customer.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Customer.Api.Controllers;

/// <summary>
/// Public endpoint for click tracking and open tracking pixels in campaign emails.
/// Exempted from user token authentication because it is accessed anonymously by email recipients
/// clicking links or loading images in sent emails; authenticity is verified by token lookup and signature.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public")]
public sealed class PublicTrackingController : ControllerBase
{
    private readonly CampaignRepository _repo;

    // 1x1 transparent PNG
    private static readonly byte[] TransparentPixel = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAA=");

    public PublicTrackingController(CampaignRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("c/{token}")]
    public async Task<IActionResult> TrackClick(string token, CancellationToken ct)
    {
        string? targetUrl = await _repo.RecordClickAsync(token, ct);
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return NotFound("Invalid or expired tracking link.");
        }

        return Redirect(targetUrl);
    }

    [HttpGet("track/open/{token}.png")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> TrackOpen(string token, CancellationToken ct)
    {
        await _repo.RecordOpenAsync(token, ct);
        return File(TransparentPixel, "image/png");
    }
}

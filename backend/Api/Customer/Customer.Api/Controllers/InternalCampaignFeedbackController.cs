using Customer.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Customer.Api.Controllers;

public sealed class CampaignDeliveryFeedbackRequest
{
    public Guid CustomerId { get; set; }
    public Guid OrgId { get; set; }
    public string MessageId { get; set; } = null!;
    public bool Delivered { get; set; }
    public bool IsHardBounce { get; set; }
    public string? Reason { get; set; }
}

[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/campaigns")]
public sealed class InternalCampaignFeedbackController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly CampaignRepository _repo;

    public InternalCampaignFeedbackController(TenantContext tenant, CampaignRepository repo)
    {
        _tenant = tenant;
        _repo = repo;
    }

    [HttpPost("delivery-feedback")]
    public async Task<IActionResult> DeliveryFeedback(
        [FromBody] CampaignDeliveryFeedbackRequest request,
        CancellationToken ct)
    {
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        await _repo.HandleDeliveryOutcomeAsync(
            request.MessageId,
            request.Delivered,
            request.IsHardBounce,
            request.Reason,
            ct);

        return Ok(new { success = true });
    }
}

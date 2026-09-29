using Customer.Entity.Models;
using Customer.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Customer.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("crm")]
[Route("api/campaigns")]
[RequireApp(App.RetailErp)]
public sealed class CampaignsController : ControllerBase
{
    private readonly CampaignRepository _repo;
    private readonly ITenantContext _tenant;

    public CampaignsController(CampaignRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    [HttpGet]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await _repo.ListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var campaign = await _repo.GetByIdAsync(id, ct);
        return campaign is null ? NotFound() : Ok(campaign);
    }

    [HttpPost]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Create([FromBody] SaveCampaignRequest request, CancellationToken ct)
    {
        var created = await _repo.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.CampaignId }, created);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveCampaignRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await _repo.UpdateAsync(id, request, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        try
        {
            bool deleted = await _repo.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/schedule")]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Schedule(long id, [FromBody] ScheduleCampaignRequest request, CancellationToken ct)
    {
        bool hasApprove = _tenant.Permissions.Contains("crm.approve") ||
                          User.HasClaim(c => c.Type == "permission" && c.Value == "crm.approve");

        try
        {
            var scheduled = await _repo.ScheduleAsync(id, request, hasApprove, ct);
            return Ok(scheduled);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/send-now")]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> SendNow(long id, CancellationToken ct)
    {
        bool hasApprove = _tenant.Permissions.Contains("crm.approve") ||
                          User.HasClaim(c => c.Type == "permission" && c.Value == "crm.approve");

        try
        {
            var sent = await _repo.SendNowAsync(id, hasApprove, ct);
            return Ok(sent);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/cancel")]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        try
        {
            var cancelled = await _repo.CancelAsync(id, ct);
            return Ok(cancelled);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}/recipients")]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> ListRecipients(
        long id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var recipients = await _repo.ListRecipientsAsync(id, page, pageSize, ct);
        return Ok(recipients);
    }

    [HttpGet("{id:long}/links")]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> ListLinks(long id, CancellationToken ct)
    {
        var links = await _repo.ListLinksAsync(id, ct);
        return Ok(links);
    }
}

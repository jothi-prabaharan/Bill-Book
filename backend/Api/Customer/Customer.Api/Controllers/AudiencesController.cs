using Customer.Entity.Models;
using Customer.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Customer.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("crm")]
[Route("api/audiences")]
[Route("api/campaigns/audiences")]
[RequireAddOn(AddOn.Crm)]
public sealed class AudiencesController : ControllerBase
{
    private readonly AudienceRepository _repo;

    public AudiencesController(AudienceRepository repo)
    {
        _repo = repo;
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
        var audience = await _repo.GetByIdAsync(id, ct);
        return audience is null ? NotFound() : Ok(audience);
    }

    [HttpPost]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Create([FromBody] SaveAudienceRequest request, CancellationToken ct)
    {
        var created = await _repo.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.AudienceId }, created);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveAudienceRequest request, CancellationToken ct)
    {
        var updated = await _repo.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:long}")]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        bool deleted = await _repo.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("preview")]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> Preview([FromBody] AudienceRules rules, CancellationToken ct)
    {
        var preview = await _repo.ResolvePreviewAsync(rules, ct);
        return Ok(preview);
    }

    [HttpPost("{id:long}/preview")]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> PreviewExisting(long id, CancellationToken ct)
    {
        var audience = await _repo.GetByIdAsync(id, ct);
        if (audience is null) return NotFound();

        var preview = await _repo.ResolvePreviewAsync(audience.Rules, ct);
        return Ok(preview);
    }

    [HttpPost("count")]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> Count([FromBody] AudienceRules rules, CancellationToken ct)
    {
        var preview = await _repo.ResolvePreviewAsync(rules, ct);
        return Ok(new { count = preview.TotalSubscribedRecipients, suppressedCount = preview.TotalSuppressedRecipients });
    }

    [HttpGet("{id:long}/recipients")]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> GetRecipients(long id, CancellationToken ct)
    {
        var audience = await _repo.GetByIdAsync(id, ct);
        if (audience is null) return NotFound();

        var preview = await _repo.ResolvePreviewAsync(audience.Rules, ct);
        return Ok(preview);
    }
}

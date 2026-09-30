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
[Route("api/automations")]
[Route("api/campaigns/automations")]
[RequireAddOn(AddOn.Crm)]
public sealed class AutomationsController : ControllerBase
{
    private readonly AutomationRepository _repo;

    public AutomationsController(AutomationRepository repo)
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
        var automation = await _repo.GetByIdAsync(id, ct);
        return automation is null ? NotFound() : Ok(automation);
    }

    [HttpPost]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Create([FromBody] SaveAutomationRequest request, CancellationToken ct)
    {
        var created = await _repo.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.AutomationId }, created);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveAutomationRequest request, CancellationToken ct)
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

    [HttpGet("{id:long}/enrolments")]
    [RequirePermission("crm.view")]
    public async Task<IActionResult> ListEnrolments(long id, CancellationToken ct)
    {
        var enrolments = await _repo.ListEnrolmentsAsync(id, ct);
        return Ok(enrolments);
    }
}

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
[Route("api/email-templates")]
[Route("api/campaigns/templates")]
[RequireAddOn(AddOn.Crm)]
public sealed class EmailTemplatesController : ControllerBase
{
    private readonly EmailTemplateRepository _repo;

    public EmailTemplatesController(EmailTemplateRepository repo)
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
        var template = await _repo.GetByIdAsync(id, ct);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpPost]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Create([FromBody] SaveEmailTemplateRequest request, CancellationToken ct)
    {
        var created = await _repo.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.EmailTemplateId }, created);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("crm.edit")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveEmailTemplateRequest request, CancellationToken ct)
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
}

using Claims.Api.Services;
using Claims.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Claims.Api.Controllers;

[ApiController]
[Authorize]
[RequireApp(App.Hrms)]
[RequireModulePermission("claims")]
[Route("api/clm/categories")]
public sealed class ClaimCategoriesController : ControllerBase
{
    private readonly ClaimService _claimService;

    public ClaimCategoriesController(ClaimService claimService) => _claimService = claimService;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        Ok(await _claimService.GetCategoriesAsync(includeInactive, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var category = await _claimService.GetCategoryByIdAsync(id, ct);
        return category is null ? NotFound() : Ok(category);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClaimCategoryRequest req, CancellationToken ct)
    {
        var created = await _claimService.CreateCategoryAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = created.ClaimCategoryId }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] CreateClaimCategoryRequest req, CancellationToken ct)
    {
        var updated = await _claimService.UpdateCategoryAsync(id, req, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}

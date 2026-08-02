using Inventory.Api.Services;
using Inventory.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Ordering;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/item-categories")]
public sealed class ItemCategoriesController : ControllerBase
{
    private readonly ItemCategoryService _categories;

    public ItemCategoriesController(ItemCategoryService categories) => _categories = categories;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _categories.ListAsync(includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveItemCategoryRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _categories.CreateAsync(request, ct),
            () => StatusCode(StatusCodes.Status201Created));

    [HttpPut("{categoryId:long}")]
    public async Task<IActionResult> Update(
        long categoryId, [FromBody] SaveItemCategoryRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _categories.UpdateAsync(categoryId, request, ct), NoContent);

    [HttpDelete("{categoryId:long}")]
    public async Task<IActionResult> Delete(long categoryId, CancellationToken ct) =>
        InventoryResponses.From(this, await _categories.DeleteAsync(categoryId, ct), NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _categories.ReorderAsync(request, ct), NoContent);
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Inventory.Repository.Repositories;
using Inventory.Entity.TableEntities;
using Shared.Kernel.Apps;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Internal;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
public class RecipeController : ControllerBase
{
    private readonly RecipeRepository _repo;

    public RecipeController(RecipeRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("menuitem/{menuItemId}")]
    [PermissionAction("read")]
    public async Task<IActionResult> GetRecipe(long menuItemId)
    {
        var result = await _repo.GetRecipeForMenuItemAsync(menuItemId);
        return Ok(result);
    }
}

[ApiController]
[Route("api/[controller]")]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
public class WastageController : ControllerBase
{
    private readonly WastageRepository _repo;

    public WastageController(WastageRepository repo)
    {
        _repo = repo;
    }

    [HttpPost]
    [PermissionAction("create")]
    public async Task<IActionResult> ReportWastage([FromBody] WastageEntry entry)
    {
        await _repo.ReportWastageAsync(entry);
        return Ok();
    }
}

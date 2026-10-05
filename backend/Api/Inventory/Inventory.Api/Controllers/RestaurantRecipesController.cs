using Inventory.Entity.Models;
using Inventory.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Inventory.Api.Controllers;

/// <summary>Recipes (TK-149). Answers only while recipes are switched on; logic is in <see cref="RecipeRepository"/>.</summary>
[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[RequireRestaurantFeature(RestaurantFeature.Recipes)]
[Route("api/restaurant/recipes")]
public sealed class RestaurantRecipesController : ControllerBase
{
    private readonly RecipeRepository _recipes;

    public RestaurantRecipesController(RecipeRepository recipes) => _recipes = recipes;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _recipes.ListAsync(ct));

    [HttpGet("{menuItemId:long}")]
    public async Task<IActionResult> Get(long menuItemId, CancellationToken ct) =>
        await _recipes.GetAsync(menuItemId, ct) is { } recipe ? Ok(recipe) : NotFound();

    [HttpPut("{menuItemId:long}")]
    public async Task<IActionResult> Save(long menuItemId, [FromBody] SaveRecipeRequest request, CancellationToken ct) =>
        Respond(await _recipes.SaveAsync(menuItemId, request, ct), NoContent);

    [HttpDelete("{menuItemId:long}")]
    public async Task<IActionResult> Delete(long menuItemId, CancellationToken ct) =>
        Respond(await _recipes.DeleteAsync(menuItemId, ct), NoContent);

    private IActionResult Respond<T>(MenuResult<T> result, Func<IActionResult> onOk) => result.Outcome switch
    {
        MenuOutcome.Ok => onOk(),
        MenuOutcome.NotFound => NotFound(),
        _ => BadRequest(new MessageResponse { Message = result.Message! }),
    };
}

/// <summary>Wastage (TK-149): food thrown away, through a stock adjustment. Logic is in <see cref="WastageRepository"/>.</summary>
[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[RequireRestaurantFeature(RestaurantFeature.Recipes)]
[Route("api/restaurant/wastage")]
public sealed class RestaurantWastageController : ControllerBase
{
    private readonly WastageRepository _wastage;

    public RestaurantWastageController(WastageRepository wastage) => _wastage = wastage;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _wastage.ListAsync(from, to, ct));

    [HttpPost]
    [PermissionAction("create")]
    public async Task<IActionResult> Record([FromBody] RecordWastageRequest request, CancellationToken ct)
    {
        MenuResult<long> result = await _wastage.RecordAsync(request, ct);
        return result.Outcome == MenuOutcome.Ok
            ? StatusCode(StatusCodes.Status201Created, new { wastageEntryId = result.Value })
            : BadRequest(new MessageResponse { Message = result.Message! });
    }
}

/// <summary>
/// A bill's ingredients leaving stock, for Sales (TK-149), under the internal
/// key. The branch comes from the body and is set before anything is resolved.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/restaurant/recipes")]
public sealed class InternalRecipeConsumptionController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalRecipeConsumptionController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] ConsumeIngredientsRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty || request.BillId <= 0)
        {
            return BadRequest(new MessageResponse { Message = "A customer, an organization and a bill are required." });
        }

        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        (string? refusal, ConsumeIngredientsResponse? issued) = await _services.GetRequiredService<RecipeRepository>().ConsumeAsync(request, ct);
        return refusal is null ? Ok(issued) : Conflict(new MessageResponse { Message = refusal });
    }
}

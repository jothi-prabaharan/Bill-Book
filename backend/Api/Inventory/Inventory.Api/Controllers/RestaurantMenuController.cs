using Inventory.Entity.Models;
using Inventory.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tenancy;

namespace Inventory.Api.Controllers;

/// <summary>The restaurant menu (TK-144): categories, timed menus and dishes. Logic is in <see cref="RestaurantMenuRepository"/>.</summary>
[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[Route("api/restaurant/menu")]
public sealed class RestaurantMenuController : ControllerBase
{
    private readonly RestaurantMenuRepository _menu;

    public RestaurantMenuController(RestaurantMenuRepository menu) => _menu = menu;

    [HttpGet("categories")]
    public async Task<IActionResult> Categories([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _menu.ListCategoriesAsync(includeInactive, ct));

    [HttpPost("categories")]
    [PermissionAction("create")]
    public async Task<IActionResult> CreateCategory([FromBody] SaveMenuCategoryRequest request, CancellationToken ct) =>
        Respond(await _menu.SaveCategoryAsync(null, request, ct), id => StatusCode(StatusCodes.Status201Created, new { menuCategoryId = id }));

    [HttpPut("categories/{categoryId:long}")]
    public async Task<IActionResult> UpdateCategory(long categoryId, [FromBody] SaveMenuCategoryRequest request, CancellationToken ct) =>
        Respond(await _menu.SaveCategoryAsync(categoryId, request, ct), _ => NoContent());

    [HttpDelete("categories/{categoryId:long}")]
    public async Task<IActionResult> DeleteCategory(long categoryId, CancellationToken ct) =>
        Respond(await _menu.DeleteCategoryAsync(categoryId, ct), _ => NoContent());

    [HttpGet("schedules")]
    public async Task<IActionResult> Schedules(CancellationToken ct) => Ok(await _menu.ListSchedulesAsync(ct));

    [HttpPost("schedules")]
    [PermissionAction("create")]
    public async Task<IActionResult> CreateSchedule([FromBody] SaveMenuScheduleRequest request, CancellationToken ct) =>
        Respond(await _menu.SaveScheduleAsync(null, request, ct), id => StatusCode(StatusCodes.Status201Created, new { menuScheduleId = id }));

    [HttpPut("schedules/{scheduleId:long}")]
    public async Task<IActionResult> UpdateSchedule(long scheduleId, [FromBody] SaveMenuScheduleRequest request, CancellationToken ct) =>
        Respond(await _menu.SaveScheduleAsync(scheduleId, request, ct), _ => NoContent());

    [HttpDelete("schedules/{scheduleId:long}")]
    public async Task<IActionResult> DeleteSchedule(long scheduleId, CancellationToken ct) =>
        Respond(await _menu.DeleteScheduleAsync(scheduleId, ct), _ => NoContent());

    [HttpGet("items")]
    public async Task<IActionResult> Items([FromQuery] long? categoryId, [FromQuery] string? search, CancellationToken ct) =>
        Ok(await _menu.ListItemsAsync(categoryId, search, ct));

    [HttpGet("items/{itemId:long}")]
    public async Task<IActionResult> Item(long itemId, CancellationToken ct) =>
        await _menu.GetItemAsync(itemId, ct) is { } item ? Ok(item) : NotFound();

    [HttpPost("items")]
    [PermissionAction("create")]
    public async Task<IActionResult> AddItem([FromBody] SaveMenuItemRequest request, CancellationToken ct) =>
        Respond(await _menu.SaveItemAsync(null, request, ct), id => StatusCode(StatusCodes.Status201Created, new { itemId = id }));

    [HttpPut("items/{itemId:long}")]
    public async Task<IActionResult> SaveItem(long itemId, [FromBody] SaveMenuItemRequest request, CancellationToken ct) =>
        Respond(await _menu.SaveItemAsync(itemId, request, ct), _ => NoContent());

    [HttpDelete("items/{itemId:long}")]
    public async Task<IActionResult> RemoveItem(long itemId, CancellationToken ct) =>
        Respond(await _menu.RemoveItemAsync(itemId, ct), _ => NoContent());

    /// <summary>Sold out or back on. Edit, not create: staff at the pass flip it many times a night.</summary>
    [HttpPut("items/{itemId:long}/availability")]
    public async Task<IActionResult> ItemAvailability(long itemId, [FromBody] SetMenuAvailabilityRequest request, CancellationToken ct) =>
        Respond(await _menu.SetItemAvailabilityAsync(itemId, request.IsAvailable, ct), _ => NoContent());

    [HttpPut("modifiers/{modifierId:long}/availability")]
    public async Task<IActionResult> ModifierAvailability(long modifierId, [FromBody] SetMenuAvailabilityRequest request, CancellationToken ct) =>
        Respond(await _menu.SetModifierAvailabilityAsync(modifierId, request.IsAvailable, ct), _ => NoContent());

    /// <summary>The menu to order from: served at <paramref name="at"/> (branch local time, now when left out), priced for the order type.</summary>
    [HttpGet("ordering")]
    public async Task<IActionResult> Ordering([FromQuery] OrderType orderType, [FromQuery] DateTime? at, CancellationToken ct) =>
        Ok(await _menu.OrderingMenuAsync(orderType, at ?? DateTime.Now, ct));

    private IActionResult Respond<T>(MenuResult<T> result, Func<T, IActionResult> onOk) => result.Outcome switch
    {
        MenuOutcome.Ok => onOk(result.Value!),
        MenuOutcome.NotFound => NotFound(),
        _ => BadRequest(new MessageResponse { Message = result.Message! }),
    };
}

/// <summary>
/// Pricing order lines for Sales (TK-144, TK-146), under the internal key. The
/// branch comes from the body and is set before the repository is resolved.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/restaurant/menu")]
public sealed class InternalRestaurantMenuController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalRestaurantMenuController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("price")]
    public async Task<IActionResult> Price([FromBody] PriceMenuLinesRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse { Message = "A customer and an organization are required." });
        }

        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        (MenuLineRefusal? refusal, PriceMenuLinesResponse? priced) = await _services
            .GetRequiredService<RestaurantMenuRepository>()
            .PriceLinesAsync(request.OrderType, request.At, request.Lines, ct);

        return refusal is null ? Ok(priced) : UnprocessableEntity(refusal);
    }
}

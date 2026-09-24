using Master.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

[ApiController]
[Route("api/menu")]
[Authorize]
[RequireApp(App.All)]
public sealed class MenuController : ControllerBase
{
    private readonly MenuService _menuService;

    public MenuController(MenuService menuService) => _menuService = menuService;

    [HttpGet]
    public async Task<IActionResult> GetUserMenu(CancellationToken ct)
    {
        var menus = await _menuService.GetUserMenuAsync(ct);
        return Ok(menus);
    }
}
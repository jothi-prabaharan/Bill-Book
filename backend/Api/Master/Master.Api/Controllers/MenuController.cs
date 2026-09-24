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

    /// <summary>
    /// The caller's menu in the app their token is for (TK-44). The shell also
    /// sends <c>?app=</c>, but the token decides: a menu for another app would
    /// offer screens every service then refuses with 403.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUserMenu(CancellationToken ct)
    {
        App app = RequireAppAttribute.AppOf(User);
        var menus = await _menuService.GetUserMenuAsync(ct, app == App.None ? App.RetailErp : app);
        return Ok(menus);
    }
}
using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

[ApiController]
[Route("api/menu")]
[Authorize]
[RequireBusinessType(AllowStandalone = true)]
public sealed class MenuController : ControllerBase
{
    private readonly MenuRepository _menuService;

    public MenuController(MenuRepository menuService) => _menuService = menuService;

    /// <summary>
    /// The caller's menu: the branch's business type, add-ons and the caller's
    /// permissions decide it (D-36, TK-136).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUserMenu(CancellationToken ct)
    {
        var menus = await _menuService.GetUserMenuAsync(ct);
        return Ok(menus);
    }
}

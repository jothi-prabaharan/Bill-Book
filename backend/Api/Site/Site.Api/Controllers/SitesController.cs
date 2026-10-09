using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Site.Entity.TableEntities;
using Site.Repository.Repositories;

namespace Site.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/site/sites")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class SitesController : ControllerBase
{
    private readonly SiteRepository _sites;

    public SitesController(SiteRepository sites) => _sites = sites;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _sites.ListAsync(ct));

    [HttpGet("{siteId:long}")]
    public async Task<IActionResult> Get(long siteId, CancellationToken ct)
    {
        SiteJob? site = await _sites.GetAsync(siteId, ct);
        return site is null ? NotFound() : Ok(site);
    }
}

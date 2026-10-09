using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;
using Site.Repository;

namespace Site.Api.Controllers;

/// <summary>
/// Site services edit log (TK-118, TK-169). Takes <c>site.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/edit-log/site")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class EditLogController : EditLogControllerBase<SiteDbContext>
{
    public EditLogController(SiteDbContext db)
        : base(db)
    {
    }
}

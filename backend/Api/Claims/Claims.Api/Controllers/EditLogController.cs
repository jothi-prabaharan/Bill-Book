using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Claims.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Claims.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>claims.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("claims")]
[Route("api/edit-log/claims")]
[RequireAddOn(AddOn.Hrms)]
public sealed class EditLogController : EditLogControllerBase<ClaimsDbContext>
{
    public EditLogController(ClaimsDbContext db)
        : base(db)
    {
    }
}

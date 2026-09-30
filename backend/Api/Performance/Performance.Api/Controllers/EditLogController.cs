using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Performance.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Performance.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>performance.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("performance")]
[Route("api/edit-log/performance")]
[RequireAddOn(AddOn.Hrms)]
public sealed class EditLogController : EditLogControllerBase<PerformanceDbContext>
{
    public EditLogController(PerformanceDbContext db)
        : base(db)
    {
    }
}

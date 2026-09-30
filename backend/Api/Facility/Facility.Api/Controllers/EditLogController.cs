using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Facility.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Facility.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>facility.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("facility")]
[Route("api/edit-log/facility")]
[RequireApp(App.School)]
public sealed class EditLogController : EditLogControllerBase<FacilityDbContext>
{
    public EditLogController(FacilityDbContext db)
        : base(db)
    {
    }
}

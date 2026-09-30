using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeLeave.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace TimeLeave.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>leave.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("leave")]
[Route("api/edit-log/timeleave")]
[RequireApp(App.Hrms)]
public sealed class EditLogController : EditLogControllerBase<TimeLeaveDbContext>
{
    public EditLogController(TimeLeaveDbContext db)
        : base(db)
    {
    }
}

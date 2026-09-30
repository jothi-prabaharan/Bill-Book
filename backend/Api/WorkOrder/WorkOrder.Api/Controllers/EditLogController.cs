using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkOrder.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace WorkOrder.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>workorder.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("workorder")]
[Route("api/edit-log/workorder")]
[RequireBusinessType(BusinessType.School)]
public sealed class EditLogController : EditLogControllerBase<WorkOrderDbContext>
{
    public EditLogController(WorkOrderDbContext db)
        : base(db)
    {
    }
}

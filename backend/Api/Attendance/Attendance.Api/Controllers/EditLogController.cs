using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Attendance.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Attendance.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>attendance.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("attendance")]
[Route("api/edit-log/attendance")]
[RequireBusinessType(BusinessType.School)]
public sealed class EditLogController : EditLogControllerBase<AttendanceDbContext>
{
    public EditLogController(AttendanceDbContext db)
        : base(db)
    {
    }
}

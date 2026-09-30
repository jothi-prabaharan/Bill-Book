using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Employee.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Employee.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>employee.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("employee")]
[Route("api/edit-log/hrm")]
[RequireApp(App.Hrms | App.Payroll | App.School)]
public sealed class EditLogController : EditLogControllerBase<EmployeeDbContext>
{
    public EditLogController(EmployeeDbContext db)
        : base(db)
    {
    }
}

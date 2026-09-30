using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Student.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Student.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>sis.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sis")]
[Route("api/edit-log/sis")]
[RequireApp(App.School)]
public sealed class EditLogController : EditLogControllerBase<StudentDbContext>
{
    public EditLogController(StudentDbContext db)
        : base(db)
    {
    }
}

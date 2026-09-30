using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Preventive.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Preventive.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>preventive.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("preventive")]
[Route("api/edit-log/preventive")]
[RequireApp(App.School)]
public sealed class EditLogController : EditLogControllerBase<PreventiveDbContext>
{
    public EditLogController(PreventiveDbContext db)
        : base(db)
    {
    }
}

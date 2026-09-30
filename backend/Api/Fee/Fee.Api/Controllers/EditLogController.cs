using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Fee.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Fee.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>fee.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("fee")]
[Route("api/edit-log/fee")]
[RequireApp(App.School)]
public sealed class EditLogController : EditLogControllerBase<FeeDbContext>
{
    public EditLogController(FeeDbContext db)
        : base(db)
    {
    }
}

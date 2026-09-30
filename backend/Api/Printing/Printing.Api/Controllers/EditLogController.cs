using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Printing.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Printing.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>settings.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("settings")]
[Route("api/edit-log/printing")]
[RequireBusinessType(AllowStandalone = true)]
public sealed class EditLogController : EditLogControllerBase<PrintingDbContext>
{
    public EditLogController(PrintingDbContext db)
        : base(db)
    {
    }
}

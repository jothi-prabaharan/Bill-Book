using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>reports.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("reports")]
[Route("api/edit-log/reporting")]
[RequireBusinessType]
public sealed class EditLogController : EditLogControllerBase<ReportingDbContext>
{
    public EditLogController(ReportingDbContext db)
        : base(db)
    {
    }
}

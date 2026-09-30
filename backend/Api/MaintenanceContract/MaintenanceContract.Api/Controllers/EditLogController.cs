using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaintenanceContract.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace MaintenanceContract.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>amc.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("amc")]
[Route("api/edit-log/amc")]
[RequireBusinessType(BusinessType.School)]
public sealed class EditLogController : EditLogControllerBase<MaintenanceContractDbContext>
{
    public EditLogController(MaintenanceContractDbContext db)
        : base(db)
    {
    }
}

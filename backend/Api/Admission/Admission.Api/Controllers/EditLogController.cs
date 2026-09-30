using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Admission.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Admission.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>admission.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("admission")]
[Route("api/edit-log/admission")]
[RequireBusinessType(BusinessType.School)]
public sealed class EditLogController : EditLogControllerBase<AdmissionDbContext>
{
    public EditLogController(AdmissionDbContext db)
        : base(db)
    {
    }
}

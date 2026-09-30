using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Customer.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Customer.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>crm.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("crm")]
[Route("api/edit-log/customer")]
[RequireBusinessType]
[RequireAddOn(AddOn.Crm)]
public sealed class EditLogController : EditLogControllerBase<CustomerDbContext>
{
    public EditLogController(CustomerDbContext db)
        : base(db)
    {
    }
}

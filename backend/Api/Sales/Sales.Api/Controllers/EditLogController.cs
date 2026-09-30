using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>sales.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/edit-log/sales")]
[RequireApp(App.RetailErp)]
public sealed class EditLogController : EditLogControllerBase<SalesDbContext>
{
    public EditLogController(SalesDbContext db)
        : base(db)
    {
    }
}

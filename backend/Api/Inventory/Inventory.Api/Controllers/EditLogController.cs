using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Inventory.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Inventory.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>inventory.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("inventory")]
[Route("api/edit-log/inventory")]
[RequireApp(App.RetailErp)]
public sealed class EditLogController : EditLogControllerBase<InventoryDbContext>
{
    public EditLogController(InventoryDbContext db)
        : base(db)
    {
    }
}

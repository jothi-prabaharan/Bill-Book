using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Purchase.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>purchase.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/edit-log/purchase")]
[RequireBusinessType(BusinessType.General, BusinessType.Pharma, BusinessType.Jewellery)]
public sealed class EditLogController : EditLogControllerBase<PurchaseDbContext>
{
    public EditLogController(PurchaseDbContext db)
        : base(db)
    {
    }
}

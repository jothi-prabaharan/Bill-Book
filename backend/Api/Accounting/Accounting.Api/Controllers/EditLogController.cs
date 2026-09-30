using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Accounting.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>accounting.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/edit-log/accounting")]
[RequireApp(App.RetailErp)]
public sealed class EditLogController : EditLogControllerBase<AccountingDbContext>
{
    public EditLogController(AccountingDbContext db)
        : base(db)
    {
    }
}

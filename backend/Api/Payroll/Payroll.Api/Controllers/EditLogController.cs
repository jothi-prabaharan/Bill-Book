using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payroll.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Payroll.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>payroll.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("payroll")]
[Route("api/edit-log/payroll")]
[RequireApp(App.Payroll)]
public sealed class EditLogController : EditLogControllerBase<PayrollDbContext>
{
    public EditLogController(PayrollDbContext db)
        : base(db)
    {
    }
}

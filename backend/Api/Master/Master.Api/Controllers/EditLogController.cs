using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Master.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Audit;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// This service's edit log (TK-118): a document's history and the audit-log
/// report, read through the branch's query filter. Takes <c>contacts.view</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("contacts")]
[Route("api/edit-log/master")]
[RequireBusinessType(AllowStandalone = true)]
public sealed class EditLogController : EditLogControllerBase<ContactsDbContext>
{
    public EditLogController(ContactsDbContext db)
        : base(db)
    {
    }
}

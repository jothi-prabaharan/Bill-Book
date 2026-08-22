using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// Resolves contact ids to codes and names, in batches, for the services that
/// hold documents.
///
/// <b>This is the other half of a decision made in `SALES.md`.</b> A document
/// stores `ContactId` and deliberately not the name, so that correcting a
/// misspelling shows everywhere — including on documents already raised. That
/// only works if reading a list of documents is cheap, and it is only cheap if
/// the names come back in one call.
///
/// <b>POST, not GET, with the ids in the body.</b> A quote list resolves fifty or
/// two hundred ids at once; that many in a query string meets a proxy's URL
/// length limit well before the framework's, and the failure mode would be a
/// silently truncated list rather than an error.
///
/// It reads <see cref="ContactsDbContext"/> — the customer's own `con` schema,
/// not the shared master database — so the query filter scopes it to the
/// caller's branch like everything else. A service asking about another branch's
/// contact gets nothing back rather than a name it should not see.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/contacts")]
public sealed class InternalContactNamesController : ControllerBase
{
    /// <summary>A cap, so one caller cannot ask for the whole table in a single request.</summary>
    private const int MaxIds = 500;

    private readonly ContactsDbContext _db;

    public InternalContactNamesController(ContactsDbContext db) => _db = db;

    [HttpPost("names")]
    public async Task<IActionResult> Names(
        [FromBody] NameLookupRequest request, CancellationToken ct)
    {
        List<long> ids = [.. request.Ids.Distinct().Take(MaxIds)];

        if (ids.Count == 0)
        {
            return Ok(Array.Empty<NamedRef>());
        }

        List<NamedRef> names = await _db.Contacts
            .Where(c => ids.Contains(c.ContactId))
            .Select(c => new NamedRef(c.ContactId, c.ContactCode, c.DisplayName))
            .ToListAsync(ct);

        // Ids that resolved to nothing are simply absent. The caller shows an id
        // for those, which is the right answer for a contact in another branch or
        // one that no longer exists.
        return Ok(names);
    }
}

using Master.Entity.Models;
using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

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

    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalContactNamesController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("names")]
    public async Task<IActionResult> Names(
        [FromBody] NameLookupRequest request, CancellationToken ct)
    {
        // The branch comes from the body, or from the user's token when the
        // caller forwards one. Before TK-06 neither was read: the callers send
        // only the internal key, so the query filter saw no branch and every
        // name came back missing.
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse
                {
                    Message = "A customer and an organization are required to resolve names.",
                });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        List<long> ids = [.. request.Ids.Distinct().Take(MaxIds)];

        if (ids.Count == 0)
        {
            return Ok(Array.Empty<NamedRef>());
        }

        // Resolved only now, after the tenant is set: the context takes its
        // connection and its query filter from the tenant when it is built.
        var db = _services.GetRequiredService<ContactsDbContext>();

        List<NamedRef> names = await db.Contacts
            .Where(c => ids.Contains(c.ContactId))
            .Select(c => new NamedRef(c.ContactId, c.ContactCode, c.DisplayName))
            .ToListAsync(ct);

        // Ids that resolved to nothing are simply absent. The caller shows an id
        // for those, which is the right answer for a contact in another branch or
        // one that no longer exists.
        return Ok(names);
    }
    /// <summary>
    /// The address each contact is written to at: its default person's email,
    /// or else the first active person's that has one (TK-20, payment
    /// reminders). A contact with no email anywhere is absent, and the caller
    /// sends it nothing. The branch comes from the body, as for names.
    /// </summary>
    [HttpPost("emails")]
    public async Task<IActionResult> Emails(
        [FromBody] NameLookupRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse
                {
                    Message = "A customer and an organization are required to resolve addresses.",
                });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        List<long> ids = [.. request.Ids.Distinct().Take(MaxIds)];

        if (ids.Count == 0)
        {
            return Ok(Array.Empty<ContactEmail>());
        }

        var db = _services.GetRequiredService<ContactsDbContext>();

        Dictionary<long, string> names = await db.Contacts
            .Where(c => ids.Contains(c.ContactId) && c.IsActive)
            .ToDictionaryAsync(c => c.ContactId, c => c.DisplayName, ct);

        var people = await db.ContactPersons
            .Where(p => names.Keys.Contains(p.ContactId) && p.IsActive && p.Email != null && p.Email != "")
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.ContactPersonId)
            .Select(p => new { p.ContactId, p.Email })
            .ToListAsync(ct);

        List<ContactEmail> emails = [.. people
            .GroupBy(p => p.ContactId)
            .Select(g => new ContactEmail(g.Key, g.First().Email!, names[g.Key]))];

        return Ok(emails);
    }
}

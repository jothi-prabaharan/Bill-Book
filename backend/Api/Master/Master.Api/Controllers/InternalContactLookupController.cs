using Master.Entity.Models;
using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Contacts;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Master.Api.Controllers;

/// <summary>
/// Which contacts exist in a branch and what each is for (School, TK-61): Sis
/// checks a student's guardians, Fee the guardian it invoices, Amc a contract's
/// vendor. The branch comes from the body, as on every internal route, and the
/// query filter keeps the answer inside it: another branch's contact comes back
/// missing, never named.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/contacts")]
public sealed class InternalContactLookupController : ControllerBase
{
    private const int MaxIds = 500;

    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalContactLookupController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] ContactLookupRequest request, CancellationToken ct)
    {
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse { Message = "A customer and an organization are required to look up contacts." });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        List<long> ids = [.. request.Ids.Distinct().Take(MaxIds)];
        if (ids.Count == 0)
        {
            return Ok(Array.Empty<ContactSummary>());
        }

        var db = _services.GetRequiredService<ContactsDbContext>();
        List<ContactSummary> found = await db.Contacts
            .AsNoTracking()
            .Where(c => ids.Contains(c.ContactId))
            .Select(c => new ContactSummary
            {
                ContactId = c.ContactId,
                ContactCode = c.ContactCode,
                DisplayName = c.DisplayName,
                IsCustomer = c.IsCustomer,
                IsVendor = c.IsVendor,
                IsGuardian = c.IsGuardian,
                IsActive = c.IsActive,
            })
            .ToListAsync(ct);

        return Ok(found);
    }
}
